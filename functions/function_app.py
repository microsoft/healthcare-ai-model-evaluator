import gc
import json
import logging
import os
import traceback
from datetime import datetime, timezone

import azure.functions as func

from medbench.metrics import (
    calculate_exact_match_metrics,
    calculate_image_metrics,
    calculate_summarization_metrics,
)
from medbench.models import ModelRun
from medbench.config import settings

# Set azure-core logging to WARNING
logging.getLogger("azure").setLevel(logging.WARNING)

# Add at the top of your file
os.environ["TRANSFORMERS_VERBOSE"] = "1"
os.environ["HF_HUB_ENABLE_HF_TRANSFER"] = "0"  # Disable optimized transfer

app = func.FunctionApp()


@app.function_name("MetricsHealthCheck")
@app.route(route="health", auth_level=func.AuthLevel.ANONYMOUS)
def health_check(req: func.HttpRequest) -> func.HttpResponse:
    """Health check endpoint for the main metrics function app."""

    try:
        # Basic health check
        health_info = {
            "status": "healthy",
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "service": "metrics-processor",
            "version": "1.0.0",
            "features": {
                "standard_metrics": True,
                "summarization_metrics": True,
            },
            "environment": {
                "azure_openai_endpoint": bool(
                    getattr(settings, "azure_openai_endpoint", None)
                    or os.environ.get("AZURE_OPENAI_ENDPOINT")
                ),
                "azure_openai_deployment": getattr(
                    settings, "azure_openai_deployment", None
                )
                or os.environ.get("AZURE_OPENAI_DEPLOYMENT", "not-set"),
            },
        }

        return func.HttpResponse(
            json.dumps(health_info, indent=2),
            status_code=200,
            mimetype="application/json",
        )

    except Exception as e:
        error_info = {
            "status": "unhealthy",
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "error": str(e),
        }

        return func.HttpResponse(
            json.dumps(error_info, indent=2),
            status_code=500,
            mimetype="application/json",
        )


@app.function_name("MetricsProcessorContainer")
@app.blob_trigger(
    arg_name="blob", path="metricjobs/{name}", connection="AzureWebJobsStorage"
)
@app.blob_output(
    arg_name="outputBlob",
    path="metricresults/{name}-results.json",
    connection="AzureWebJobsStorage",
)
def metrics_processor(blob: func.InputStream, outputBlob: func.Out[str]):
    logging.info(
        f"Python blob trigger function processed blob \n"
        f"Name: {blob.name}\n"
        f"Size: {blob.length} bytes"
    )

    try:
        # Add logging about cache directories
        logging.info(f"TRANSFORMERS_CACHE: {os.environ.get('TRANSFORMERS_CACHE')}")
        logging.info(f"HF_HOME: {os.environ.get('HF_HOME')}")

        # Read the blob content this is a test
        blob_content = blob.read().decode("utf-8")
        model_run_data = json.loads(blob_content)

        # Parse the input data
        model_run = ModelRun.from_json(model_run_data.get("model_run", {}))

        # Calculate metrics based on the metrics_type
        metrics_type = model_run_data.get("metrics_type", "summarization")
        results = calculate_metrics(model_run, metrics_type)

        # Create output JSON with original data and metrics results
        output_data = {
            "original_run": model_run_data,
            "test": "test",
            "metrics_results": results,
            "processed_at": datetime.now(timezone.utc).isoformat(),
        }

        # Output the results to the destination container
        outputBlob.set(json.dumps(output_data, indent=2))
        logging.info(f"Successfully processed metrics for {blob.name}")

        gc.collect()

    except Exception as e:
        error_message = f"Error processing metrics: {str(e)}\n{traceback.format_exc()}"
        logging.error(error_message)
        # Still write the error to the output blob
        outputBlob.set(
            json.dumps(
                {
                    "error": error_message,
                    "original_blob_name": blob.name,
                    "processed_at": datetime.now(timezone.utc).isoformat(),
                },
                indent=2,
            )
        )


def calculate_metrics(model_run, metrics_type):
    """Calculate metrics based on the metrics type."""
    if metrics_type == "summarization":
        return calculate_summarization_metrics_with_tbfact(model_run)
    elif metrics_type == "image_quality":
        return calculate_image_metrics(model_run)
    elif metrics_type == "accuracy":
        return calculate_exact_match_metrics(model_run)
    else:
        # Default to summarization metrics
        return calculate_summarization_metrics_with_tbfact(model_run)


def calculate_summarization_metrics_with_tbfact(model_run):
    """Calculate summarization metrics including TBFact for factual consistency."""
    import asyncio
    from medbench.config import settings
    from medbench.evaluators.tbfact.runner import TBFactEvaluatorRunner
    from medbench.metrics import aggregate_metrics
    from medbench.models import OpenAIReasoningModel
    
    # First calculate standard summarization metrics
    standard_metrics = calculate_summarization_metrics(model_run)
    
    try:
        # Add TBFact metrics for factual consistency
        logging.info("Calculating TBFact metrics for summarization task")
        
        # Initialize OpenAI model for TBFact evaluation
        llm_evaluator = OpenAIReasoningModel(
            name=settings.azure_openai_deployment,
            version=settings.azure_openai_version,
            endpoint=settings.azure_openai_endpoint,
            api_key=settings.azure_openai_api_key,
            vision_enabled=False,
            system_prompt="",  # Prompts are defined by the evaluator runner
            max_tokens=40000,
            stop=None,
            stream=False,
        )
        
        # Initialize TBFact evaluator runner
        tbfact_evaluator_runner = TBFactEvaluatorRunner(
            predictions_model_run=model_run,
            evaluator=llm_evaluator,
            reference_facts_path="/tmp/tbfact_reference_facts.json",
            batch_size=5
        )
        
        # Run TBFact evaluation asynchronously
        async def run_tbfact():
            try:
                tbfact_results = await tbfact_evaluator_runner.evaluate()
            except Exception as e:
                logging.error(f"Error running TBFact evaluation: {str(e)}")
                # Return empty results if evaluation fails completely
                return []
            
            # Transform results to match standard format
            tbfact_instance_metrics = []
            for i, result in enumerate(tbfact_results):
                try:
                    # Extract TBFact metrics (f1, recall, precision)
                    metrics = result.get("details", {}).get("metrics", {})
                    instance_metrics = {
                        "tbfact-f1": metrics.get("f1", 0.0),
                        "tbfact-recall": metrics.get("recall", 0.0),
                        "tbfact-precision": metrics.get("precision", 0.0),
                    }
                    tbfact_instance_metrics.append(instance_metrics)
                except Exception as e:
                    logging.error(f"Error processing TBFact evaluation result {i}: {str(e)}")
                    # Add default metrics for failed instances
                    tbfact_instance_metrics.append(
                        {"tbfact-f1": 0.0, "tbfact-recall": 0.0, "tbfact-precision": 0.0}
                    )
            
            return tbfact_instance_metrics
        
        # Execute TBFact evaluation
        tbfact_instance_metrics = asyncio.run(run_tbfact())
        
        # Merge TBFact metrics with standard metrics
        combined_instance_metrics = []
        for i, standard_instance in enumerate(standard_metrics["instance_level_metrics"]):
            combined_instance = standard_instance.copy()
            if i < len(tbfact_instance_metrics):
                combined_instance.update(tbfact_instance_metrics[i])
            else:
                # Fallback for mismatched lengths
                combined_instance.update({"tbfact-f1": 0.0, "tbfact-recall": 0.0, "tbfact-precision": 0.0})
            combined_instance_metrics.append(combined_instance)
        
        # Calculate aggregated metrics including TBFact
        combined_metrics = {
            "aggregated_metrics": aggregate_metrics(combined_instance_metrics),
            "instance_level_metrics": combined_instance_metrics,
        }
        
        logging.info("Successfully calculated combined summarization and TBFact metrics")
        return combined_metrics
        
    except Exception as e:
        logging.error(f"Error calculating TBFact metrics: {str(e)}")
        logging.error(traceback.format_exc())
        # Fallback to standard metrics if TBFact fails
        logging.info("Falling back to standard summarization metrics due to TBFact error")
        return standard_metrics
