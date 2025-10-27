# Healthcare AI Model Evaluator Engine

**Medical AI model benchmarking engine.**

Healthcare AI Model Evaluator Engine is the Python-based evaluation backend for the **Arena** platform, Microsoft's model comparison and validation system for medical AI applications. It provides custom metrics computation, model evaluation workflows, and seamless integration with external benchmarking frameworks.

## Overview

Healthcare AI Model Evaluator Engine enables medical AI evaluation through:

- **Custom Metrics**: Advanced evaluation metrics including factual consistency (TBFact), lexical metrics (ROUGE, BERTScore), and domain-specific assessments
- **Integrated TBFact**: Factual consistency evaluation automatically included in summarization tasks
- **Model-as-Judge**: LLM-based evaluation capabilities for subjective metrics via evaluator addon
- **External Integration**: Conversion tools for MedHelm and other evaluation frameworks
- **Scalable Deployment**: Azure Function Apps for production-ready metric computation
- **Arena Integration**: Standardized data schema for seamless UI-based validation workflows

### Components

1. **Main Metrics Processor**: Standard evaluation metrics (exact match, ROUGE, BERTScore, summarization) with integrated TBFact factual consistency evaluation
2. **Evaluator Add-on**: Custom Model-as-a-Judge evaluators for specialized evaluation tasks
3. **External Framework Integration**: Notebooks for MedHelm data conversion
4. **Arena Integration**: Shared data schema and validation workflows

## Getting Started

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://docs.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
- [Docker](https://docs.docker.com/get-docker/)
- Azure subscription

### Local Development Setup

1. **Install dependencies**:
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

2. **Configure environment**:
   ```bash
   cp .env.example .env
   # Edit .env with your Azure OpenAI credentials
   ```

3. **Start local services**:
   ```bash
   # In the project root
   docker compose up azurite

   # Start local functions
   cd ./functions
   docker compose up
   ```

# Healthcare AI Model Evaluator Developer Guide

This guide provides instructions for setting up and deploying Healthcare AI Model Evaluator function apps with an improved developer experience.

## Architecture Overview

Healthcare AI Model Evaluator now consists of a main Azure Function app with an optional evaluator addon:

1. **Metrics Function App** (`haime-metrics`) - Handles general metrics processing with integrated TBFact factual consistency evaluation
2. **Evaluator Function App** (`haime-evaluator`) - Optional addon for Model-as-a-Judge evaluations

The main function app now includes TBFact evaluation directly within summarization tasks, while the evaluator addon demonstrates how to build custom evaluator workflows that operate independently.

## Function Apps

### Metrics Function App

**Triggers**:
- Blob trigger on `metricjobs/` container
  - Outputs to `metricresults/` container
- HTTP endpoint for health checks

**Supported Metrics**:
- Exact match metrics
- Image similarity metrics  
- Text summarization metrics with integrated TBFact factual consistency evaluation

**Input Format**:
Sample input is available under `doc/sample-data`:
- [Sample ACI Bench input output pair for evaluation](../doc/sample-data/sample_acibench_summarization_metricjobs_input.json)

### Evaluator Function App (Add-on)

**Triggers**:
- Blob trigger on `evaluatorjobs/` container (separate from main metrics)
  - Outputs to `evaluatorresults/` container
- HTTP endpoint for health checks

**Input Format**:
Sample inputs are available under `doc/sample-data`:
- [Sample A/B testing input](../doc/sample-data/sample_ab_testing_evaluatorjobs_input.json)
- [Sample simple validation input](../doc/sample-data/sample_simple_validation_evaluatorjobs_input.json)
