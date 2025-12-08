# Healthcare AI Model Evaluator Deployment Guide

This guide covers deploying the complete Healthcare AI Model Evaluator platform including the frontend, backend, and Azure Functions for metrics processing.

## Overview

Healthcare AI Model Evaluator consists of:
- **Frontend**: React-based web application (served from .NET API)
- **Backend**: .NET API (Container App)
- **Metrics Functions**: Python-based Azure Functions for evaluation processing
  - **Main Metrics Processor**: Docker-based function with TBFact integration
  - **Evaluator Addon**: Custom model-as-judge evaluators

## Getting Started

### Prerequisites

> [!IMPORTANT]
> Follow the steps in order. Each step builds on the previous ones.

**Required Software:**
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-docker/) (for Functions deployment)
- DotNet v8.0.318

**Azure Subscription Requirements:**
- **Azure OpenAI**: Access to one of the supported models for Model-as-a-Judge with Metrics Azure Functions.
  - Recommended: 100K TPM (tokens per minute) quota
  - You may select the model that best suit your needs.
- **Azure Functions**: Premium or dedicated plan quota is required for containerized functions.
- **Cosmos DB**: Availability varies by region. If you encounter capacity issues, try a different region (see troubleshooting).
- **Container Apps**: Available in most Azure regions
- **Permissions**: 
   - A resource group where you have _Owner_ permissions for deployment (subscription-level owner permissions is OK too).
   - **Application Developer** role (or higher) in Entra ID to create App Registrations for authentication.

### Step 1: Verify Prerequisites (Quota & Availability)
* **Azure OpenAI**
  - Ensure you have quota for your desired model in your `AZURE_GPT_LOCATION` region (recommended: 100K-200K TPM).

**Required Permissions:**

* **Azure Resource Access**
  - You need **Owner** rights on at least one resource group to provision resources.
  - You need **Application Developer** role (or higher) in Entra ID to create and configure App Registrations for authentication.
  - If you lack subscription-level owner permissions, ask your IT administrator to:
    - Create a resource group for you
    - Grant you the **Owner** role on that specific resource group
    - Grant you **Application Developer** role in Entra ID (or create the App Registration for you)
    - Use that resource group in **Step 3**

### Step 2: Create an `azd` Environment & Configure Settings

First, authenticate with Azure services:

```sh
# Log in to Azure CLI and Azure Developer CLI
az login                 # add --tenant <TENANT_ID> if needed
azd auth login           # add --tenant <TENANT_ID> if needed
```

Create a new environment with a short name:

```sh
# Create environment (keep name ≤ 8 characters for best results)
azd env new <envName>
```

#### Azure OpenAI Configuration

During deployment (`azd up`), you'll be prompted to select an Azure OpenAI model, capacity, and deployment type. You can review available models at the [Azure OpenAI Service models documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/reasoning).

The model selection follows the format: `name;version` (e.g., `o3-mini;2025-01-31`).

**Choose your deployment approach:**

| Variable | When to Use | Description |
|----------|-------------|-------------|
| `CREATE_AZURE_OPENAI=true` (default) | You want to create a new Azure OpenAI service | Deployment will provision a new Azure OpenAI service with your selected model |
| `CREATE_AZURE_OPENAI=false` | You have an existing Azure OpenAI service | Use an existing service instead of creating a new one (requires endpoint and API key) |

**To use an existing Azure OpenAI service**, set these variables before running `azd up`:

```sh
# Configure to use existing Azure OpenAI service
azd env set CREATE_AZURE_OPENAI false
azd env set EXISTING_AZURE_OPENAI_ENDPOINT "https://your-openai.openai.azure.com/"
azd env set EXISTING_AZURE_OPENAI_KEY "your-api-key"
```

#### Optional Configuration

**Regional Quota Flexibility**: If you have limited quota in your primary region, you can deploy specific resources to alternate regions:

```sh
# Example: Deploy Azure OpenAI to a different region
azd env set AZURE_GPT_LOCATION eastus2

# Example: Deploy Azure Functions to a different region  
azd env set AZURE_FUNCTIONS_LOCATION westus3

# Example: Deploy to a supported region
azd env set AZURE_LOCATION westus2

# Other location overrides if needed:
# azd env set AZURE_KEYVAULT_LOCATION centralus
# azd env set AZURE_COSMOS_LOCATION westus
# azd env set AZURE_STORAGE_LOCATION eastus
# azd env set AZURE_CONTAINER_REGISTRY_LOCATION westus2
```

> [!TIP]
> Only set location overrides if you have quota constraints. Most deployments work fine with a single region.

> [!NOTE]
> All components can be deployed to most Azure regions. Choose a region that supports Container Apps and Azure Functions.

**Feature Flags**: Control optional components:

```sh
# Disable evaluator addon to reduce deployment time
azd env set ENABLE_EVALUATOR_ADDON false

# Disable Azure Communication Services
azd env set ENABLE_ACS false
```

### Step 3: Deploy the Infrastructure

Now that your environment is configured, you can deploy all necessary resources and infrastructure for the Healthcare AI Model Evaluator.

#### IP Filtering & Security Configuration

**By default, the deployment is secure-by-default** and will prompt you to configure IP filtering to protect the web application:

- **During first deployment**, you'll be prompted to enter an IP address that can access the web application
- **Your current public IP** is automatically detected and suggested as the default
- **Only specified IPs** can access the web application - all other access is blocked
- **Backend data services** (Cosmos DB, Storage) are secured via managed identity and are not directly accessible from the internet

**Managing IP Access:**

```sh
# View current IP filtering settings
azd env get-value ALLOWED_WEB_IP
azd env get-value ENABLE_WEB_IP_FILTERING

# Add or update allowed IPs (comma-delimited CIDR format)
azd env set ALLOWED_WEB_IP "89.144.197.27/32,203.0.113.1/32"

# Disable IP filtering entirely (not recommended for production)
azd env set ENABLE_WEB_IP_FILTERING false

# Re-deploy with new settings
azd up
```

> [!WARNING]
> **Portal Changes**: Any IP filtering changes made directly in the Azure portal will be overwritten by `azd up`. Always use the azd environment variables to manage IP access.

> [!TIP]
> **Multiple Locations**: Use comma-delimited CIDR notation to allow access from multiple locations: `"home.ip.address/32,office.ip.address/32,vpn.range.address/24"`

> [!IMPORTANT]
> **Security Consideration**: For production deployments in healthcare environments, consider integrating with your existing Azure Front Door after deployment. See the [Security Configuration](#security-configuration) section for Front Door integration steps.

> [!IMPORTANT]
> Deploying the infrastructure will create Azure resources in your subscription and may incur costs.

To start the deployment process, run:

   ```bash
   azd up
   ```

During deployment you will be prompted for any required variable not yet set, such as subscription, resource group and location.

This command will:
- Provision all Azure infrastructure (Container Apps, Functions, Storage, Cosmos DB, etc.)
- Deploy backend API (with integrated frontend)
- Create or configure Azure OpenAI service
- Set up shared blob storage for all components
- Configure authentication via Entra ID App Registration

> [!TIP]
> For persistent deployment issues, use `azd down --purge` to completely reset your environment and manually delete the resource group to avoid Azure's soft-delete complications.

> [!IMPORTANT]
> The full deployment takes 15-20 minutes to complete. If you encounter any issues, see the [Troubleshooting](#troubleshooting) section below.

### Step 4: Build and Push Metrics Function Docker Image

   This step is necessary because `azd` does not automatically build and push Docker images for Azure Functions.

   ```bash
# From the root folder, get the registry name and endpoint
AZURE_CONTAINER_REGISTRY_NAME=$(azd env get-value AZURE_CONTAINER_REGISTRY_NAME)
AZURE_CONTAINER_REGISTRY_ENDPOINT=$(azd env get-value AZURE_CONTAINER_REGISTRY_ENDPOINT)

# Navigate to the functions folder
cd functions

# Login to Azure Container Registry
az acr login --name $AZURE_CONTAINER_REGISTRY_NAME

# Build the Docker image
docker compose build medbench-metrics

# Tag the image for the registry
docker tag functions-medbench-metrics:latest $AZURE_CONTAINER_REGISTRY_ENDPOINT/medbench-metrics:latest

# Push the image to Azure Container Registry
docker push $AZURE_CONTAINER_REGISTRY_ENDPOINT/medbench-metrics:latest
   ```

> [!NOTE]
> After pushing the image, the Azure Function will automatically pull and deploy it. This may take a few minutes.

---

## Verification

After deployment completes, verify your resources are running:

```bash
# Get deployment outputs
azd env get-values

# Check function app status
az functionapp list --output table

# Get your application URL (frontend and API served from same endpoint)
echo "Application URL: $(azd env get-value API_BASE_URL)"
echo "Frontend: $(azd env get-value API_BASE_URL)/webapp"
echo "API: $(azd env get-value API_BASE_URL)/api"
```

## Post-Deployment Setup

### Create First Admin User

After successful deployment, create your first admin user to access the application:

```bash
# Run the admin user creation script
./infra/scripts/create-admin-user.sh
```

The script will prompt you for:
- **Admin email address**: Used for login
- **Admin password**: Must meet complexity requirements (8+ characters, 3 of 4 character types)  
- **Admin full name**: Display name in the application

Once created, you can:
1. Navigate to your application URL: `$(azd env get-value API_BASE_URL)/webapp`
2. Click "Sign in with Password" 
3. Use the email/password you just created
4. Access the admin panel to create additional users

> **Note**: This script only needs to be run once. Additional users can be created through the web interface by admin users.

---

## Architecture Overview

### Components

**Frontend**: React-based web application served from the .NET API at `/webapp`
- User interface for model evaluation management
- Authentication via Entra ID

**Backend API**: .NET 8 API deployed to Azure Container Apps
- RESTful API for evaluation orchestration
- Cosmos DB for data persistence
- Azure Storage for file management

**Metrics Functions**: Python-based Azure Functions for evaluation processing

1. **Main Metrics Processor** (Docker-based)
   - Purpose: Standard evaluation metrics (ROUGE, BERTScore, exact match) + TBFact factual consistency
   - Deployment: Docker container in Premium V3 plan
   - Triggers: Blob uploads to `metricjobs` container
   - Outputs: Results in `metricresults` container
   - Outputs: Results in `metricresults` container

2. **Evaluator Addon** (Optional, Python zip package)
   - Purpose: Custom model-as-judge evaluators
   - Triggers: Blob uploads to `evaluatorjobs` container  
   - Outputs: Results in `evaluatorresults` container

### Storage Containers

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
echo "Metrics Function: $(azd env get-value METRICS_FUNCTION_APP_URL)"
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

## Security Configuration

### Protecting Your Deployment from Public Access

For production healthcare environments, you should restrict access to your application. There are several approaches depending on your existing infrastructure and security requirements.

### Integrate with Existing Azure Front Door

Most healthcare organizations already have Azure Front Door with WAF configured. You can integrate MedBench behind your existing Front Door.

#### Configure Container Apps for Front Door Integration

After deployment, configure your Container App to accept traffic only from your existing Front Door:

```bash
# Get your deployment details
RESOURCE_GROUP=$(azd env get-value AZURE_RESOURCE_GROUP)
CONTAINER_APP_NAME="api-$(azd env get-value AZURE_ENV_NAME)"

# Get your existing Front Door's service tag or backend pool IP
# Replace with your Front Door's actual service tag
FRONT_DOOR_ID="AzureFrontDoor.Backend"

# Restrict Container App ingress to Front Door only
az containerapp ingress access-restriction add \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --rule-name "FrontDoorOnly" \
  --service-tag $FRONT_DOOR_ID \
  --action "Allow" \
  --description "Allow traffic only from existing Front Door"

# Block all other traffic
az containerapp ingress access-restriction add \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --rule-name "DenyAll" \
  --ip-address-range "0.0.0.0/0" \
  --action "Deny" \
  --priority 1000 \
  --description "Deny all other traffic"
```

#### Add HAIME Backend to Your Front Door

Add the deployed Container App as a backend in your existing Front Door configuration:

```bash
# Get the Container App URL (without https://)
BACKEND_HOST=$(azd env get-value API_BASE_URL | sed 's|https://||')

# Add to your Front Door backend pool
az network front-door backend-pool backend add \
  --front-door-name "your-existing-frontdoor" \
  --pool-name "your-backend-pool" \
  --resource-group "your-frontdoor-rg" \
  --address $BACKEND_HOST \
  --http-port 80 \
  --https-port 443 \
  --priority 1 \
  --weight 50
```

#### Update Front Door Routing Rules

Configure routing to send MedBench traffic to the new backend:

```bash
# Create routing rule for MedBench
az network front-door routing-rule create \
  --front-door-name "your-existing-frontdoor" \
  --resource-group "your-frontdoor-rg" \
  --name "medbench-routing" \
  --frontend-endpoints "your-frontend" \
  --route-type Forward \
  --backend-pool "your-backend-pool" \
  --patterns "/medbench/*" \
  --accepted-protocols Https
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

1. **Location does not support desired model**
   > InvalidResourceProperties: The specified SKU 'Standard' of account deployment is not supported by the model

   If you have this error, try changing the region of the Azure OpenAI resource to another region that supports the model you want to deploy

2. **Cosmos DB Capacity Issues**
   > ServiceUnavailable: Database account creation failed... high demand in [region]

   Change the Cosmos DB region:
   ```bash
   azd env set AZURE_COSMOS_LOCATION westus2
   azd up
   ```

4. **Function Deployment Failures**
   ```bash
   # Check Docker is running
   docker info
   
   # Rebuild and redeploy
   azd deploy
   ```

5. **Storage Access Issues**
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