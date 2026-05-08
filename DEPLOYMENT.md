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

This project supports two deployment networking modes:

- `DEPLOYMENT_NETWORKING=open` (default): public ingress
- `DEPLOYMENT_NETWORKING=private`: internal-only networking (VNet)

When you run `azd up`, the preprovision hook will prompt you to choose `open` vs `private`.

**Private mode (VNet, internal-only)**

In `private` mode:

- The Container Apps Environment is deployed as internal-only and the API ingress is not publicly exposed.
- A **Private Endpoint** is created for the Container Apps Environment.
- A **DNS Private Resolver** inbound endpoint is created for VPN clients.
- Private DNS zones are created for:
  - `privatelink.<region>.azurecontainerapps.io`
  - `<defaultDomain>` (app FQDNs)
- Azure Functions are configured for regional VNet integration (outbound).
- You must access the API from within the VNet (for example via VPN, jumpbox, or peered network).
- **Post-provision note:** the `postprovision` hook writes secrets to Key Vault. If Key Vault public access is disabled (default in private mode), you must be connected to the VNet (VPN/bastion/peered) for `azd up` to complete in one shot.

**Accessing private mode (VPN / institutional network)**

For step-by-step instructions to reach the internal-only endpoint (Point-to-Site VPN Gateway for individual access, or peering/S2S for institutional networks), see:

- [docs/private_network_access_vpn.md](docs/private_network_access_vpn.md)

For a quick setup of a Point-to-Site VPN Gateway (OpenVPN + Microsoft Entra ID), you can also run:

```sh
./infra/scripts/create-p2s-vpn-gateway.sh
```

To force private mode up-front:

```sh
azd env set DEPLOYMENT_NETWORKING private
```

You can either create a new VNet (recommended) or use an existing one:

```sh
# Create a new VNet + required subnets
azd env set CREATE_VNET true

# OR: use existing subnets (resource IDs)
azd env set CREATE_VNET false
azd env set EXISTING_VNET_RESOURCE_ID "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>"
azd env set EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<aca-subnet>"
azd env set EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<functions-subnet>"
azd env set EXISTING_PRIVATE_ENDPOINT_SUBNET_ID "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<private-endpoints-subnet>"
```

#### Portal walkthrough: create VNet + required subnets (for CREATE_VNET=false)

If you want to create the VNet and subnets yourself in the Azure Portal, follow these steps. This is useful when you need to deploy into an existing network or apply custom controls.

1) **Create (or select) a VNet**
   - Azure Portal → Virtual networks → Create
   - Address space: choose a CIDR that does not overlap your on-prem or VPN client ranges (example: `10.30.0.0/16`).
   - Resource group and region should match your azd deployment.

2) **Create required subnets** (VNet → Subnets → + Subnet)
   - **aca-infra** (Container Apps Environment infrastructure)
     - Address range: e.g. `10.30.0.0/23`
     - Subnet delegation: `Microsoft.App/environments`
   - **functions-integration** (Azure Functions VNet integration)
     - Address range: e.g. `10.30.2.0/24`
     - Subnet delegation: `Microsoft.Web/serverFarms`
   - **private-endpoints** (Private Endpoint subnet)
     - Address range: e.g. `10.30.4.0/27`
     - Private endpoint network policies: **Disabled**
   - **dns-resolver-inbound** (DNS Private Resolver inbound endpoint) (optional but recommended)
     - Address range: e.g. `10.30.3.0/27`
     - Subnet delegation: `Microsoft.Network/dnsResolvers`
   - **GatewaySubnet** (only if you plan to use P2S VPN Gateway)
     - Address range: e.g. `10.30.255.0/27`
     - Name must be exactly `GatewaySubnet`.

3) **Copy subnet resource IDs**
   - Each subnet blade → Copy **Resource ID**.
   - Set these in your azd env (see the commands above). For private endpoints, set `EXISTING_PRIVATE_ENDPOINT_SUBNET_ID`.

4) **Run azd provision**
   - Your deployment will create private endpoints and DNS zones using the subnets you provided.

> Tip: If `azd up` fails at `postprovision` with a Key Vault network error, connect to the VNet and re-run `./infra/scripts/postprovision.sh`.

> Note: If you disable private endpoints (`CREATE_PRIVATE_ENDPOINT=false`), you can skip the private-endpoints subnet and the private DNS zones. If you keep private endpoints enabled, private DNS is required for name resolution inside the VNet.


**Optional IP filtering (open mode)**

In `open` mode, no IP filtering is auto-configured.

If you want to restrict API ingress by IP, you can enable it explicitly:
**By default, the deployment is secure-by-default** and will prompt you to configure IP filtering to protect the web application:

- **During first deployment**, you'll be prompted to enter an IP address that can access the web application
- **Your current public IP** is automatically detected and suggested as the default (using preprovision hooks)
- **Only specified IPs** can access the web application - all other access is blocked at the Container App ingress level
- **Backend data services** (Cosmos DB, Storage) use public endpoints but are secured via managed identity authentication and connection strings
- **IP filtering applies only to the Container App** - Azure Functions, Storage, and Cosmos DB are secured through Azure's service-to-service authentication and managed identities

**Managing IP Access:**

```sh
# View current IP filtering settings
azd env get-value ALLOWED_WEB_IP
azd env get-value ENABLE_WEB_IP_FILTERING

# Add or update allowed IPs (comma-delimited CIDR format)
azd env set ALLOWED_WEB_IP "89.144.197.27/32,203.0.113.1/32"


# Enable IP filtering (comma-delimited CIDR format)
azd env set ENABLE_WEB_IP_FILTERING true
azd env set ALLOWED_WEB_IP "203.0.113.1/32,198.51.100.0/24"

# Disable IP filtering
# Disable IP filtering entirely (not recommended for production)
azd env set ENABLE_WEB_IP_FILTERING false

# Open to the entire internet 

azd env set ENABLE_WEB_IP_FILTERING false
azd env set ALLOWED_WEB_IP ""

# Re-deploy with new settings
azd up
```

> [!WARNING]
> Setting `DEPLOYMENT_NETWORKING=open` exposes the API publicly unless you add your own ingress controls (IP filtering, gateway, etc.).

**Quick commands**

```sh
# Development: Open mode, no IP filtering
azd env set DEPLOYMENT_NETWORKING open
azd env set ENABLE_WEB_IP_FILTERING false
azd up

# Production-ish: Open mode + lock down to office IP
azd env set DEPLOYMENT_NETWORKING open

azd env set ENABLE_WEB_IP_FILTERING true
azd env set ALLOWED_WEB_IP "your.office.ip.address/32"
azd up

# Multiple locations: Home + Office access
azd env set ALLOWED_WEB_IP "home.ip.address/32,office.ip.address/32"
azd up
```

> [!WARNING]
> **Portal Changes**: Any IP filtering changes made directly in the Azure portal will be overwritten by `azd up`. Always use the azd environment variables to manage IP access.

> [!NOTE]
> **Cosmos DB auth**: This deployment uses **Azure Cosmos DB SQL API** with **local auth disabled** (no keys / no connection strings). The app authenticates using **Microsoft Entra ID / Managed Identity** via `DefaultAzureCredential`.

> [!TIP]
> **Local Cosmos emulation**: For local development, you can run the Azure Cosmos DB Emulator (Linux) in Docker and set `COSMOSDB_CONNECTION_STRING` with the emulator connection string plus `COSMOSDB_DATABASE=HAIMEDB`.

> [!WARNING]
> **Legacy migration (Mongo → SQL API)**: Older deployments used **Cosmos DB API for MongoDB**. This repository now provisions **Cosmos DB SQL API** instead, which means **existing Mongo data is not automatically migrated**. Plan for a one-time migration or accept data loss when moving environments.

> **Export (old Mongo API only — not for local dev)**
> - Use `mongoexport` against your old Cosmos Mongo API account (or any Mongo-compatible endpoint):
>   ```sh
>   mongoexport --uri "$MONGO_URI" --db HAIMEDB --collection Users --jsonArray --out Users.json
>   mongoexport --uri "$MONGO_URI" --db HAIMEDB --collection Experiments --jsonArray --out Experiments.json
>   # Repeat for other collections: TestScenarios, ClinicalTasks, Images, DataSets, DataObjects, Trials, Models
>   ```
>
> **Import (new SQL API)**
> - Ensure your current identity has Cosmos **data-plane** permissions on the new account (for example, the built-in “Cosmos DB Built-in Data Contributor” role). You can grant this in the Azure Portal, or use Azure CLI to discover role definition IDs and assign them.
>
> **Azure CLI (recommended) – grant yourself Cosmos SQL data access**
> ```sh
> # Resolve resource group + Cosmos account name from your azd environment
> RG="$(azd env get-value AZURE_RESOURCE_GROUP_NAME)"
> COSMOS_ACCOUNT="$(azd env get-value AZURE_COSMOS_ACCOUNT_NAME)"
>
> # Your Entra ID object id (requires directory read permissions in your tenant)
> PRINCIPAL_ID="$(az ad signed-in-user show --query id -o tsv)"
>
> # Discover the built-in Data Contributor role definition id for this Cosmos account
> ROLE_DEF_ID="$(az cosmosdb sql role definition list \
>   --resource-group "$RG" \
>   --account-name "$COSMOS_ACCOUNT" \
>   --query "[?roleName=='Cosmos DB Built-in Data Contributor'].id | [0]" \
>   -o tsv)"
>
> # Assign the role at account scope (data-plane)
> az cosmosdb sql role assignment create \
>   --resource-group "$RG" \
>   --account-name "$COSMOS_ACCOUNT" \
>   --principal-id "$PRINCIPAL_ID" \
>   --role-definition-id "$ROLE_DEF_ID" \
>   --scope "/"
> ```
>
> **Notes**
> - For local dev + migration scripts, you typically assign the role to your **signed-in user** (command above).
> - For runtime access, this template assigns Cosmos SQL data-plane RBAC to the deployed workload identity (managed identity/service principal) during provisioning.
> - Import each exported file using the helper script in `tools/cosmos-migration/`:
>   ```sh
>   python3 -m pip install -r tools/cosmos-migration/requirements.txt
>
>   python3 tools/cosmos-migration/import_mongo_export.py \
>     --endpoint "$(azd env get-value COSMOSDB_ENDPOINT)" \
>     --database "$(azd env get-value COSMOSDB_DATABASE)" \
>     --container Users \
>     --input Users.json
>   ```
>
> **Mapping details**
> - The import script maps Mongo `_id` → Cosmos `id` (string) and removes `_id`.
> - Containers are partitioned by `/id` in this deployment, so `id` must be present and a string.
> - Import per container: `Users`, `Models`, `Experiments`, `ClinicalTasks`, `TestScenarios`, `DataObjects`, `DataSets`, `Images`, `Trials`.
> - The import script also normalizes common **Mongo Extended JSON** wrappers (for example `$oid`, `$date`, `$numberLong`) into plain values so the .NET app can read them back cleanly.
> - Cosmos SQL queries are **case-sensitive** for property names. Keep existing document field casing (for example `Email`, `UserId`, `ExperimentId`, `TestScenarioId`, `TaskId`, `Status`).


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
echo "Frontend: $(azd env get-value API_BASE_URL)
echo "API: $(azd env get-value API_BASE_URL)/api"
```

## Post-Deployment Setup

### Create First Admin User

You can bootstrap the first admin user automatically during deployment by setting these azd environment values:

```bash
azd env set ROOT_ADMIN_EMAIL "admin@example.com"
azd env set ROOT_ADMIN_NAME "Admin User"
azd env set ROOT_ADMIN_PASSWORD "<strong-password>"
azd up
```

Once created, you can:
1. Navigate to your application URL: `$(azd env get-value API_BASE_URL)/webapp`
2. Click "Sign in with Password" 
3. Use the email/password you just created
4. Access the admin panel to create additional users

> **Note**: This only needs to be set once. Additional users can be created through the web interface by admin users.


---

## Architecture Overview

### Components

**Frontend**: React-based web application served from the .NET API at `/`
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

4. **Cannot Access the Application (403 Forbidden)**
   > This typically means your IP address is not in the allowed list

   Check your current IP and update the allowed list:
   ```bash
   # Check what IP you're accessing from
   curl ifconfig.me
   
   # View current filtering settings
   azd env get-value ENABLE_WEB_IP_FILTERING
   azd env get-value ALLOWED_WEB_IP
   
   # Update with your current IP
   azd env set ALLOWED_WEB_IP "$(curl -s ifconfig.me)/32"
   azd up
   
   # Or temporarily disable for troubleshooting (development only)
   azd env set ENABLE_WEB_IP_FILTERING false
   azd up
   ```

5. **IP Filtering Not Working as Expected**
   ```bash
   # Verify Container App ingress rules in Azure Portal
   az containerapp ingress show \
     --name "api-$(azd env get-value AZURE_ENV_NAME)" \
     --resource-group "$(azd env get-value AZURE_RESOURCE_GROUP)"
   
   # Check if changes were applied (restart may be needed)
   azd up
   ```

6. **Function Deployment Failures**
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
# Create .env file from template (required for docker compose)
cp .env.example .env
# Edit .env with your Azure OpenAI credentials if needed

docker compose up
```

This starts:
- Azurite storage emulator
- Both function apps in development mode
- Shared development environment

See the [functions README](functions/README.md) for detailed local development instructions. 