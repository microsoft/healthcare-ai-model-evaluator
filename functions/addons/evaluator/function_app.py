"""
Custom Evaluator Function App - Dedicated function app for Summary (Model as a Judge) evaluations.
"""

import asyncio
import json
import logging
import os
import traceback
from datetime import datetime, timezone

import attrs
import azure.functions as func
from medbench.config import settings

# Import MedBench Summary Evaluator implementation
from medbench.evaluators import ABEvaluatorRunner, SummaryEvaluatorRunner
from medbench.models import ModelRun, OpenAIReasoningModel

# Set azure-core logging to WARNING
logging.getLogger("azure").setLevel(logging.WARNING)

app = func.FunctionApp()


async def run_summary_evaluator(model_run, llm_evaluator, questions_generator_runner=None, output_instructions=""):
    """
    Run SummaryEvaluatorRunner.
    
    Args:
        model_run: The model run to evaluate
        llm_evaluator: The LLM evaluator model
        questions_generator_runner: Optional pre-existing questions generator
        output_instructions: Optional output specifications to inject
        
    Returns:
        SummaryEvaluatorRunner: The completed evaluator instance
    """
    try:
        # Initialize SummaryEvaluatorRunner
        kwargs = {}
        
        # Use provided questions generator if available
        if questions_generator_runner is not None:
            kwargs["questions_generator_runner"] = questions_generator_runner

        # Inject output instructions if provided
        if output_instructions:
            kwargs["output_specs_prompt"] = output_instructions

        evaluator = SummaryEvaluatorRunner(
            predictions_model_run=model_run,
            evaluator=llm_evaluator,
            skip_errors=True,
            **kwargs,
        )

        # Run summary evaluation
        await evaluator.evaluate()

        # Return the evaluator itself
        return evaluator

    except Exception as e:
        logging.error(f"Error in Summary evaluation: {str(e)}")
        raise


async def _process_model_run_async(
    model_run, llm_evaluator, questions_generator_runner=None, output_instructions=""
):
    """Helper function to process model run using SummaryEvaluatorRunner with injected output instructions."""
    try:
        evaluator = await run_summary_evaluator(
            model_run, llm_evaluator, questions_generator_runner, output_instructions
        )
        # Extract the evaluation result text
        return evaluator.evaluator_runner._model_run.results[0].completions.get_text()
    except Exception as e:
        logging.error(f"Error in Summary evaluation: {str(e)}")
        raise


async def _process_ab_testing_async(model_run, llm_evaluator, output_instructions):
    """Helper function to process A/B testing using SummaryEvaluatorRunner + MultimodalEvaluatorRunner."""
    try:
        # Step 1: Run vanilla SummaryEvaluatorRunner for each model separately
        model_runs = []
        questions_generator_runner = None  # Placeholder for questions generator if needed
        for i, result in enumerate(model_run.results):
            # Create a single-model run for each result
            single_model_run = attrs.evolve(
                model_run, id=f"{model_run.id}_model_{i}", results=[result]
            )

            # Process with vanilla SummaryEvaluatorRunner using the shared function
            evaluator = await run_summary_evaluator(
                single_model_run, llm_evaluator, questions_generator_runner
            )

            questions_generator_runner = evaluator.questions_generator_runner

            # Extract the evaluator runner's model run for AB comparison
            model_runs.append(evaluator.evaluator_runner._model_run)

        # Step 2: Use ABEvaluatorRunner to compare the outputs according to output_instructions
        comparison_runner = ABEvaluatorRunner(
            predictions_model_run=model_runs[0],
            predictions_model_run_b=model_runs[1],
            evaluator=llm_evaluator,
            output_specs_prompt=output_instructions,
        )

        await comparison_runner.evaluate()
        return comparison_runner.evaluator_runner._model_run.results[0].completions.get_text()

    except Exception as e:
        logging.error(f"Error in A/B testing evaluation: {str(e)}")
        raise


@app.function_name("SummaryEvaluator")
@app.blob_trigger(
    arg_name="blob", path="evaluatorjobs/{name}", connection="AzureWebJobsStorage"
)
@app.blob_output(
    arg_name="outputBlob",
    path="evaluatorresults/{name}-results.json",
    connection="AzureWebJobsStorage",
)
def summary_evaluator(blob: func.InputStream, outputBlob: func.Out[str]):
    """
    Process Summary evaluation requests from Arena backend.

    Expected input format:
    {
        "model_run": {
            "dataset": {...},
            "results": [...]
        },
        "function_type": "evaluator",
        "job_id": "...",
        "base_prompt": "...",
        "output_instructions": "..."
    }
    """

    logging.info(f"Summary evaluator processing blob: {blob.name}")

    try:
        # Read and parse input
        blob_content = blob.read().decode("utf-8")
        request_data = json.loads(blob_content)

        # Parse the model run data
        model_run_data = request_data.get("model_run", {})
        model_run = ModelRun.from_json(model_run_data)

        # Extract output instructions for injection into evaluator
        output_instructions = request_data.get("output_instructions", "")

        # Initialize OpenAI model for Summary evaluation
        # Use MedBench config settings with fallback to environment variables
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

        # Check if this is A/B testing (Arena experiment)
        is_ab_testing = len(model_run.results) > 1

        if is_ab_testing:
            # Process A/B testing with separate function
            evaluation_result = asyncio.run(
                _process_ab_testing_async(model_run, llm_evaluator, output_instructions)
            )
        else:
            # Process single model evaluation
            evaluation_result = asyncio.run(
                _process_model_run_async(
                    model_run, llm_evaluator, None, output_instructions
                )
            )

        # Prepare output in expected format
        output = {
            "output": evaluation_result,
            "processed_at": datetime.now(timezone.utc).isoformat(),
            "job_id": request_data.get("job_id", blob.name),
        }

        # Write result
        outputBlob.set(json.dumps(output, indent=2))

        logging.info(f"Summary evaluation completed for {blob.name}")

    except Exception as e:
        error_message = f"{str(e)}\n{traceback.format_exc()}"
        logging.error(f"Error processing Summary evaluation: {error_message}")

        # Write error result
        error_output = {
            "error": error_message,
            "original_blob_name": blob.name,
            "processed_at": datetime.now(timezone.utc).isoformat(),
            "job_id": request_data.get("job_id", blob.name)
            if "request_data" in locals()
            else blob.name,
        }

        outputBlob.set(json.dumps(error_output, indent=2))
        raise


@app.function_name("SummaryHealthCheck")
@app.route(route="health", auth_level=func.AuthLevel.ANONYMOUS)
def health_check(req: func.HttpRequest) -> func.HttpResponse:
    """Health check endpoint for the Summary evaluator function app."""

    try:
        # Basic health check
        health_info = {
            "status": "healthy",
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "service": "summary-evaluator",
            "version": "1.0.0",
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
        return func.HttpResponse(
            json.dumps(error_info, indent=2),
            status_code=500,
            mimetype="application/json",
        )
