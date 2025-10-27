# Healthcare AI Model Evaluator Deployment Guide

This guide covers deploying the complete Healthcare AI Model Evaluator platform including the frontend, backend, and Azure Functions for metrics processing.

## Overview

Healthcare AI Model Evaluator consists of:
- **Frontend**: React-based web application (Static Web App)
- **Backend**: .NET API (Container App)
- **Metrics Functions**: Python-based Azure Functions for evaluation processing
  - **Main Metrics Processor**: Docker-based function with TBFact integration
  - **Evaluator Addon**: Custom model-as-judge evaluators

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://docs.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
- [Docker](https://docs.docker.com/get-docker/) (for functions deployment)
- Azure subscription with permissions to create resources

## Quick Start

### 1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd healthcare-ai-model-evaluator
   ```

### 2. **Login and Initialize Azure Development Environment**:
   ```
   azd auth login
   azd init
   ```

### 3. Configure  the `azd` environment variables
   ```
   # Append template contents to existing .env without overwriting
   cat .env.example >> .azure/{env_name}/.env
   ```

   Note that after running `azd up` the .env will be rewritten.

   Edit `.env` file with your preferences:

   ```bash
   # Basic configuration
   AZURE_ENV_NAME=haime-dev
   AZURE_LOCATION=eastus
   AZURE_SUBSCRIPTION_ID=your-subscription-id

   # Azure OpenAI Configuration
   CREATE_AZURE_OPENAI=true  # Set to false to use existing service

   # If using existing Azure OpenAI (when CREATE_AZURE_OPENAI=false):
   # EXISTING_AZURE_OPENAI_ENDPOINT=https://your-openai-service.openai.azure.com/
   # EXISTING_AZURE_OPENAI_KEY=your-api-key

   # Function configuration
   ENABLE_EVALUATOR_ADDON=true  # Set to false to skip evaluator addon
   ```

   Further sections have more details on customizing specific configurations.

### 5. Deploy Everything

   ```bash
   azd up
   ```

   You may be prompted for some information (e.g.: resource group) when running this command.

   This single command will:
   - Provision all Azure infrastructure
   - Deploy frontend and backend applications
   - Build and deploy Azure Functions (zip-deployed functions only)
   - Create or configure Azure OpenAI service
   - Set up shared blob storage for all components

### 6. Build and push metrics function docker image

   This step is necessary because `bicep` does not take care of building and pushing docker images to registry.

   ```bash
# From the root folder, get the registry name and endpoint
AZURE_CONTAINER_REGISTRY_NAME=$(azd env get-value AZURE_CONTAINER_REGISTRY_NAME)
AZURE_CONTAINER_REGISTRY_ENDPOINT=$(azd env get-value AZURE_CONTAINER_REGISTRY_ENDPOINT)

# Navigate to the `functions` folder
cd functions

# Login to Azure Container Registry using the environment variable
az acr login --name $AZURE_CONTAINER_REGISTRY_NAME

# Build the image using docker-compose and tag it for the registry
docker compose build medbench-metrics
docker tag functions-medbench-metrics:latest $AZURE_CONTAINER_REGISTRY_ENDPOINT/medbench-metrics:latest

# Push the image to the container registry
docker push $AZURE_CONTAINER_REGISTRY_ENDPOINT/medbench-metrics:latest
   ```

## Azure OpenAI Configuration

You can find more information on available models at [Azure OpenAI Service models](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/concepts/models?tabs=global-standard%2Cstandard-chat-completions).

### Option 1: Create New Azure OpenAI Service (Default)

```bash
# In .env file:
CREATE_AZURE_OPENAI=true
AZURE_OPENAI_DEPLOYMENT=o3-mini
AZURE_OPENAI_MODEL_NAME=o3-mini
AZURE_OPENAI_MODEL_VERSION=
```

### Option 2: Use Existing Azure OpenAI Service

```bash
# In .azure/{env_name}/.env file:
CREATE_AZURE_OPENAI=false
EXISTING_AZURE_OPENAI_ENDPOINT=https://your-openai-service.openai.azure.com/
EXISTING_AZURE_OPENAI_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=your-deployment-name
```

### Option 3: Configure via Environment Variables

```bash
# Override defaults without editing .azure/{env_name}/.env
azd env set CREATE_AZURE_OPENAI false
azd env set EXISTING_AZURE_OPENAI_ENDPOINT "https://your-service.openai.azure.com/"
azd env set EXISTING_AZURE_OPENAI_KEY "your-api-key"
azd up
```

## Function Components

### Main Metrics Processor
- **Purpose**: Standard evaluation metrics (ROUGE, BERTScore, exact match) + TBFact factual consistency
- **Deployment**: Docker container
- **Triggers**: Blob uploads to `metricjobs` container
- **Outputs**: Results in `metricresults` container

### Evaluator Addon (Optional)
- **Purpose**: Custom model-as-judge evaluators
- **Deployment**: Python zip package
- **Triggers**: Blob uploads to `evaluatorjobs` container  
- **Outputs**: Results in `evaluatorresults` container

## Storage Containers

The shared storage account includes:

**Application containers:**
<!-- - `medical-images` - Medical image storage -->
- `images` - General image storage  
- `reports` - Generated reports

**Function containers:**
- `metricjobs` - Input jobs for main metrics processor
- `metricresults` - Output results from main metrics processor
- `evaluatorjobs` - Input jobs for evaluator addon
- `evaluatorresults` - Output results from evaluator addon

## Testing the Deployment

### 1. Verify Function Apps

```bash
# Check function app status
az functionapp list --output table

# Get function app URLs
azd env get-value FUNCTION_APP_URL
```

### 2. Test Metrics Processing

Upload a sample evaluation job:

```bash
# For main metrics processor
az storage blob upload \
  --account-name $(azd env get-value STORAGE_ACCOUNT_NAME) \
  --container-name metricjobs \
  --name sample-job.json \
  --file functions/examples/model_run_sample.json

# For evaluator addon (if enabled)
az storage blob upload \
  --account-name $(azd env get-value STORAGE_ACCOUNT_NAME) \
  --container-name evaluatorjobs \
  --name sample-evaluator-job.json \
  --file functions/examples/model_run_sample.json
```

### 3. Monitor Processing

```bash
# Check function logs
az functionapp logs tail \
  --name $(azd env get-value METRICS_FUNCTION_APP_NAME) \
  --resource-group $(azd env get-value AZURE_RESOURCE_GROUP)
```

### 4. Download Results

```bash
# Download results
az storage blob download \
  --account-name $(azd env get-value STORAGE_ACCOUNT_NAME) \
  --container-name metricresults \
  --name sample-job-results.json \
  --file ./results.json
```

## Configuration Options

### Disable Evaluator Addon

```bash
azd env set ENABLE_EVALUATOR_ADDON false
azd up
```

### Change Azure OpenAI Model

```bash
azd env set AZURE_OPENAI_DEPLOYMENT "gpt-35-turbo"
azd env set AZURE_OPENAI_MODEL_NAME "gpt-35-turbo"
azd env set AZURE_OPENAI_MODEL_VERSION "0613"
azd up
```

### Update Function Docker Image

```bash
azd env set DOCKER_IMAGE_TAG "v2.0"
azd up
```

## Troubleshooting

### Common Issues

1. **Azure OpenAI Quota Issues**
   ```bash
   # Use existing service instead
   azd env set CREATE_AZURE_OPENAI false
   azd env set EXISTING_AZURE_OPENAI_ENDPOINT "your-endpoint"
   azd env set EXISTING_AZURE_OPENAI_KEY "your-key"
   azd up
   ```

1. REVIEWME: **Location does not support desired model**
   > InvalidResourceProperties: The specified SKU 'Standard' of account deployment is not supported by the model

   If you have this error, try changing the region of the Azure OpenAI resource to another region that supports the model you want to deploy


2. **Function Deployment Failures**
   ```bash
   # Check Docker is running
   docker info
   
   # Rebuild and redeploy
   azd deploy
   ```

3. **Storage Access Issues**
   ```bash
   # Verify storage account exists
   az storage account show --name $(azd env get-value STORAGE_ACCOUNT_NAME)
   ```

### Getting Help

- Check function app logs in Azure Portal
- Review deployment logs: `azd logs`
- Verify resource group in Azure Portal
- Check Key Vault for stored secrets

## Clean Up

To remove all resources:

```bash
azd down --purge
```

This will delete the entire resource group and all contained resources.

## Local Development

For local development of functions:

```bash
cd functions
docker-compose up
```

This starts:
- Azurite storage emulator
- Both function apps in development mode
- Shared development environment

See the [functions README](functions/README.md) for detailed local development instructions. 