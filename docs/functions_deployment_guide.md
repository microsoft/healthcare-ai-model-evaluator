# Healthcare AI Model Evaluator Metrics Engine Deployment Guide

This guide walks you through deploying Healthcare AI Model Evaluator Metrics Engine for medical AI model benchmarking. Healthcare AI Model Evaluator Metrics Engine provides the Python-based evaluation backend for the Arena platform, enabling custom metrics computation and model evaluation workflows.

## Overview

Healthcare AI Model Evaluator Metrics Engine consists of:
- **Main Metrics Function App**: Handles standard metrics (exact match, ROUGE, BERTScore, summarization) with integrated TBFact factual consistency evaluation
- **Evaluator Add-on Function App**: Demonstrates custom Model-as-a-Judge evaluator integration for specialized evaluation tasks
- **Shared Storage**: Blob containers for job processing and results
- **Container Registry**: For Docker-based function app deployment

## Prerequisites

Before starting, ensure you have:
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [Azure Developer CLI](https://docs.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) installed
- [Docker](https://docs.docker.com/get-docker/) installed and running
- Azure subscription with appropriate permissions
- An existing Azure Resource Group (or permissions to create one)

## Quick Start

For a complete deployment with default settings:

```bash
azd up
```

This single command will:
1. Provision Azure infrastructure (Function Apps, Storage, Container Registry)
2. Build and push Docker images
3. Deploy main metrics function app (with integrated TBFact) and optional evaluator add-on
4. Configure networking and security

For more control over the deployment process, follow the step-by-step instructions below.

## Step-by-Step Deployment

### Step 1: Verify Tool Installation

Check that all required tools are installed:

```bash
# Check Azure CLI
az --version

# Check Azure Developer CLI
azd version

# Check Docker
docker --version
```

### Step 2: Authenticate with Azure

Log in to both Azure CLI and Azure Developer CLI:

```bash
# Login to Azure CLI
az login

# Login to Azure Developer CLI
azd auth login
```

### Step 3: Initialize Azure Developer Environment

Navigate to the Healthcare AI Model Evaluator project directory and initialize the Azure Developer CLI environment:

```bash
cd healthcare-ai-model-evaluator

# Initialize azd environment (choose "local" as environment name)
azd init

# Set your Azure configuration
azd env set AZURE_LOCATION "westeurope"  # or your preferred region
azd env set AZURE_SUBSCRIPTION_ID "your-subscription-id"
azd env set AZURE_RESOURCE_GROUP "your-resource-group-name"
```

**To get your subscription ID:**
```bash
az account show --query id --output tsv
```

### Step 4: Configure Environment Variables

#### Required for Main Function App

The main function app now includes integrated TBFact functionality and requires Azure OpenAI configuration:

```bash
# Copy and edit the environment file
cp .env.example .env
# Edit .env with your Azure OpenAI credentials:
# AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
# AZURE_OPENAI_API_KEY=your-api-key
# AZURE_OPENAI_DEPLOYMENT=gpt-4
# AZURE_OPENAI_VERSION=2024-02-01
```

#### Optional: Deploy Evaluator Add-on

The evaluator add-on is optional and provides Model-as-a-Judge evaluations:

```bash
# Enable evaluator add-on (enabled by default)
azd env set enableEvaluatorAddon true
```

### Step 5: Deploy Infrastructure and Applications

#### Option A: Single Command Deployment

```bash
azd up
```

This handles the complete deployment process automatically.

#### Option B: Step-by-Step Deployment

For more control or troubleshooting, use the manual process:

##### Step 5a: Provision Azure Infrastructure

```bash
azd provision
```

This creates:
- Azure Container Registry (ACR) for Docker images
- Storage Account for blob triggers and outputs
- App Service Plan (Linux Premium) for hosting function apps
- Main metrics Function App (with integrated TBFact)
- Evaluator add-on Function App (if enabled)

##### Step 5b: Build and Deploy Applications

```bash
azd deploy
```

This will:
- Build Docker images for function apps
- Push images to the provisioned ACR
- Deploy applications and update configurations

> **Note**: Both function apps include health check endpoints (`/api/health`) for monitoring. The main metrics function app uses blob triggers for processing, while the evaluator addon provides both blob triggers and HTTP endpoints.

## Architecture Overview

Healthcare AI Model Evaluator includes a main function app and an optional evaluator addon to demonstrate different approaches to custom evaluation:

1. **Main Metrics Processor** (`haime-metrics`):
   - Processes general metrics (text exact match, image metrics, summarization)
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

## Local Development

For local development we use Docker Compose.

```bash
# Copy environment template
cp .env.example .env

# Edit .env with your Azure OpenAI credentials
# (Required for main function app's integrated TBFact and evaluator addon)

# Start all services
docker-compose up
```

This starts:
- Azurite storage emulator (ports 10000-10002)
- Main metrics function app (port 7071) - includes TBFact integration
- Evaluator function app (port 7073) - Model-as-a-Judge evaluations

## Configuration

### Environment Variables

Key configuration options in `.env`:

```bash
# Azure OpenAI (required for main function app's integrated TBFact and evaluator addon)
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=o3
AZURE_OPENAI_VERSION=2024-02-01

# Storage connection for local development
AzureWebJobsStorage=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...
```

### Deployment Parameters

Customize deployment via `main.bicep` parameters:

- `environmentName`: Base name for Azure resources

## Custom Metrics Integration Pattern

The main function app with integrated TBFact demonstrates the recommended pattern for integrating custom metrics:

1. **Blob Triggers**: Use blob storage triggers for asynchronous processing
2. **Standardized Input/Output**: Follow consistent JSON schema for inputs and outputs
3. **Health Checks**: Include health check endpoints for monitoring
4. **Configuration**: Environment-based configuration for different deployments

## Next Steps

- Explore the existing metrics implementations in `medbench/metrics/`
- Review the TBFact evaluator code in `medbench/evaluators/tbfact/`
- Consider implementing additional custom metrics using the integrated TBFact pattern
- Set up monitoring and alerting for production deployments
