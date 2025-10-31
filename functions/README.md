# Healthcare AI Model Evaluator Engine

**Medical AI model benchmarking engine.**

Healthcare AI Model Evaluator Engine is the Python-based evaluation backend for the **Arena** platform, Microsoft's model comparison and validation system for medical AI applications. It provides custom metrics computation, model evaluation workflows, and seamless integration with external benchmarking frameworks.

## Overview

Healthcare AI Model Evaluator _Metrics Engine_ enables medical AI evaluation through:

- **Custom Metrics**: Advanced evaluation metrics including factual consistency (TBFact), lexical metrics (ROUGE, BERTScore), and domain-specific assessments
- **Integrated TBFact**: Factual consistency evaluation automatically included in summarization tasks
- **Model-as-Judge**: LLM-based evaluation capabilities for subjective metrics via evaluator addon
- **External Integration**: Conversion tools for MedHelm and other evaluation frameworks
- **Scalable Deployment**: Azure Function Apps for production-ready metric computation
- **Arena Integration**: Standardized data schema for seamless UI-based validation workflows

## Architecture Overview

Healthcare AI Model Evaluator _Metrics Engine_ includes a main function app and an optional evaluator addon to demonstrate different approaches to custom evaluation:

1. **Main Metrics Processor** (`haime-metrics`):
   - Processes **general metrics** (text exact match, image metrics, summarization)
   - **Integrated TBFact**: Now includes factual consistency evaluation directly in summarization tasks
   - Handles blob triggers from `metricjobs/` container
   - Outputs results to `metricresults/` container

2. **Evaluator Add-on** (`haime-evaluator`):
   - Specialized for Model-as-a-Judge evaluations using LLM-based evaluation
   - Design Purpose: Demonstrates how custom evaluator function apps can work independently
   - Monitors `evaluatorjobs/` container for evaluation requests
   - Only processes jobs where `metrics_type` is `"summarization"`
   - Outputs results to `evaluatorresults/` with naming pattern `{name}-summary-results.json`

The evaluator addon uses separate containers (`evaluatorjobs` and `evaluatorresults`) to demonstrate how different evaluation workflows can be completely isolated.

### Metrics Function App

**Triggers**:
- Blob trigger on `metricjobs/` container
  - Outputs to `metricresults/` container
- HTTP endpoint for health checks

**Input Format**:
Sample input is available under `docs/sample-data`:
- [Sample ACI Bench input output pair for evaluation](../docs/sample-data/sample_acibench_summarization_metricjobs_input.json)

### Evaluator Function App (Add-on)

**Triggers**:
- Blob trigger on `evaluatorjobs/` container (separate from main metrics)
  - Outputs to `evaluatorresults/` container
- HTTP endpoint for health checks

**Input Format**:
Sample inputs are available under `docs/sample-data`:
- [Sample A/B testing input](../docs/sample-data/sample_ab_testing_evaluatorjobs_input.json)
- [Sample simple validation input](../docs/sample-data/sample_simple_validation_evaluatorjobs_input.json)

### Custom Metrics Integration Pattern

The main function app with integrated TBFact demonstrates the recommended pattern for integrating custom metrics:

1. **Blob Triggers**: Use blob storage triggers for asynchronous processing
2. **Standardized Input/Output**: Follow consistent JSON schema for inputs and outputs
3. **Health Checks**: Include health check endpoints for monitoring
4. **Configuration**: Environment-based configuration for different deployments

### See Also:
- Explore the existing metrics implementations in `medbench/metrics/`
- Review the TBFact evaluator code in `medbench/evaluators/tbfact/`
- Consider implementing additional custom metrics using the integrated TBFact pattern
- Set up monitoring and alerting for production deployments

## Local Development Setup

### Step 1. Install dependencies:
> [!TIP]
> We recommend using an isolated Python virtual environment.

```bash
pip install -r pre-requirements.txt
pip install -r requirements.txt
```

**Requirements Setup:**

We are currently using a simple `requirements.txt` set up that still allow us to separate `dev` and `prod` dependencies as well as separate primary dependencies (those we use directly) from secondary dependencies (dependencies from our dependencies):

```
.
├── requirements
│   ├── constraints.txt # Pinned versions of dependencies (`pip freeze` output)
│   ├── common.txt      # Primary dependencies shared across environments
│   ├── dev.txt         # Primary dependencies for Dev environment
│   ├── prod.txt        # Primary dependencies for Prod environment
│   └── addon.txt       # Overritten by addons to inject extra dependencies
└── requirements.txt    # Convenience entrypoint for actual dependencies
```

### Step 2. Configure environment:
```bash
cp .env.example .env
# Edit .env with your Azure OpenAI credentials
```

### Step 3. Start local services:
```bash
# In the project root
docker compose up -d azurite

# Start local functions
cd ./functions
docker compose up
```