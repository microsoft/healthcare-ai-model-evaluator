# Healthcare AI Model Evaluator

Healthcare AI Model Evaluator is a medical AI model benchmarking platform with integrated evaluation engine to assist multi-disciplinary healthcare teams build and validate AI systems.

## Overview
A comprehensive web application for evaluating the quality of Generative AI models in medical contexts, featuring:

- **Arena Interface**: Web-based model comparison and validation
- **Integrated Metrics Engine**: Azure Functions for automated evaluation processing
- **Advanced Evaluation**: TBFact factual consistency + custom model-as-judge evaluators
- **Medical Focus**: Specialized metrics and workflows for healthcare AI

## Deployment Options

### 🚀 Azure Deployment

Deploy the complete Healthcare AI Model Evaluator platform with a single command using Azure Developer CLI (azd).

#### Prerequisites
- [Azure Developer CLI (azd)](https://docs.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Docker](https://docs.docker.com/get-docker/) (for Azure Functions deployment)
- An Azure subscription with appropriate permissions

#### Quick Start

1. **Clone the repository**
   ```bash
   git clone [your-repository-url]
   cd healthcare-ai-model-evaluator
   cp .env.example .env
   ```

1. **Initialize Azure Development Environment**:
   ```
   azd init
   ```

1. Copy the `.env.template` to the `azd` environment
   ```
   cp .env.template .azure/{env_name}/.env
   ```

1. **Edit .env file** with your preferences:
   ```bash
   # Basic configuration
   AZURE_ENV_NAME=haime-dev
   AZURE_LOCATION=eastus
   AZURE_SUBSCRIPTION_ID=your-subscription-id

   # Azure OpenAI (creates new service by default)
   CREATE_AZURE_OPENAI=true
   AZURE_OPENAI_DEPLOYMENT=gpt-4

   # Function configuration
   ENABLE_EVALUATOR_ADDON=true
   ```

1. **Deploy everything**
   ```bash
   azd up
   ```

This deploys the complete platform including frontend, backend, and Azure Functions with shared storage and Azure OpenAI configuration.

#### What Gets Deployed

The `azd up` command deploys:

**Core Platform:**
- **Azure Static Web App** - React frontend for Arena interface.
- **Azure Container App** - .NET API backend.
- **Azure Cosmos DB** - MongoDB API for data storage.
- **Azure Blob Storage** - Medical images, reports, and evaluation workflows.

**Evaluation Engine:**
- **Metrics Function App** - Docker-based evaluation processing with TBFact integration
- **Evaluator Function App** - Custom model-as-judge evaluators (optional)
- **Azure OpenAI Service** - For LLM-based evaluation (or use existing)

**Infrastructure:**
- **Azure Container Registry** - Function container images
- **Azure Key Vault** - Secure configuration management
- **Log Analytics & App Insights** - Monitoring and observability

#### Azure OpenAI Configuration

**Option 1: Create New Service (Default)**
```bash
# In .env file:
CREATE_AZURE_OPENAI=true
AZURE_OPENAI_DEPLOYMENT=gpt-4
```

**Option 2: Use Existing Service**
```bash
# In .env file:
CREATE_AZURE_OPENAI=false
EXISTING_AZURE_OPENAI_ENDPOINT=https://your-openai-service.openai.azure.com/
EXISTING_AZURE_OPENAI_KEY=your-api-key
```

**Option 3: Environment Variables**
```bash
azd env set CREATE_AZURE_OPENAI false
azd env set EXISTING_AZURE_OPENAI_ENDPOINT "your-endpoint"
azd env set EXISTING_AZURE_OPENAI_KEY "your-key"
azd up
```

<!-- #### Testing the Deployment

Upload a sample evaluation job:
```bash
# Test metrics processing
az storage blob upload \
  --account-name $(azd env get-values | grep STORAGE_ACCOUNT_NAME | cut -d'=' -f2) \
  --container-name metricjobs \
  --name sample.json \
  --file functions/examples/model_run_sample.json

# Monitor processing
az functionapp logs tail \
  --name $(azd env get-values | grep METRICS_FUNCTION_APP_NAME | cut -d'=' -f2)
``` -->

#### Deployment Configuration Options

**Disable Evaluator Addon:**

To know more about the evaluator addon refer to its [readme](./functions/addons/evaluator/README.md).

The evaluator addon is enabled by default, to disable it do:
```bash
azd env set ENABLE_EVALUATOR_ADDON false
azd up
```

**Change OpenAI Model:**
```bash
azd env set AZURE_OPENAI_DEPLOYMENT "gpt-35-turbo"
azd up
```

#### Supported Azure Regions

Choose regions that support all required services:
- `eastus` ✅ (Recommended)
- `westus2` ✅
- `eastus2` ✅
- `centralus` ✅
- `westeurope` ✅

#### Troubleshooting

**Azure OpenAI Quota Issues:**
```bash
# Use existing service instead
azd env set CREATE_AZURE_OPENAI false
azd env set EXISTING_AZURE_OPENAI_ENDPOINT "your-endpoint"
azd env set EXISTING_AZURE_OPENAI_KEY "your-key"
azd up
```

**Function Deployment Issues:**
```bash
# Ensure Docker is running
docker info

# Rebuild and redeploy
azd deploy
```

**Complete Cleanup:**
```bash
azd down --purge
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed deployment instructions and troubleshooting.

---

## Local Development

<!-- ### Prerequisites
- Node.js (v18 or higher)
- .NET 8.0 SDK
- MongoDB or SQL Server (optional if using database)
- Docker (optionally for running CosmosDB and Azuer Storage emulators) -->

### Frontend setup
```bash
cd frontend
npm install
npm run start
```

### Backend Setup
```bash
cd backend
dotnet restore
dotnet run
```

<!-- #### Backend Setup (.NET)

1. **Clone the repository**
   ```bash
   git clone [your-repository-url]
   cd backend
   ```

2. **Configure secrets using dotnet user-secrets**
   ```bash
   # Set connection strings for local development
   dotnet user-secrets set "CosmosDb:ConnectionString" "your-cosmosdb-connection-string" --project ./src/MedBench.API/
   dotnet user-secrets set "AzureStorage:ConnectionString" "your-azure-storage-connection-string" --project ./src/MedBench.API/
   ```

> **NOTE**: When running CosmosDB and Azuer Storage emulators from docker images, the connection strings are: 
> - For CosmosDB: `mongodb://localhost:10255/?ssl=true&replicaSet=globaldb&retrywrites=false&maxIdleTimeMS=120000&appName=@localhost@`
> - For Azurite: `DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://localhost:10000/devstoreaccount1;QueueEndpoint=http://localhost:10001/devstoreaccount1;TableEndpoint=http://localhost:10002/devstoreaccount1;`

4. **Run the backend**
   ```bash
   dotnet restore
   dotnet run --project ./src/MedBench.API/
   ``` -->

### Functions Setup
```bash
# From project's root
docker-compose up azurite

cd functions
docker-compose up
```

## Architecture

### Frontend (React)
- **Framework**: React 18 with TypeScript
- **Authentication**: MSAL (Microsoft Authentication Library)
- **UI**: Modern Arena interface for model comparison
- **Build**: Vite for fast development

### Backend (.NET)
- **Framework**: .NET 8 Web API
- **Database**: MongoDB (via Cosmos DB MongoDB API)
- **Authentication**: Azure AD integration
- **Storage**: Azure Blob Storage integration

### Evaluation Engine (Python Functions)
- **Metrics Processor**: Standard metrics (ROUGE, BERTScore) + TBFact factual consistency
- **Evaluator Addon**: Custom model-as-judge evaluators
- **Triggers**: Blob storage events for automated processing
- **Outputs**: Structured evaluation results for Arena validation

### Infrastructure
- **Hosting**: Container Apps + Static Web Apps + Azure Functions
- **Database**: Azure Cosmos DB (MongoDB API, serverless)
- **Storage**: Shared Azure Blob Storage with function triggers
- **AI Services**: Azure OpenAI for LLM-based evaluation
- **Security**: Key Vault + Managed Identity throughout
- **Monitoring**: Log Analytics + Application Insights

## Integration with External Frameworks

Healthcare AI Model Evaluator integrates with external evaluation frameworks:
- **MedHelm**: Data conversion utilities in `functions/notebooks/`
- **BabelBench**: Shared dataset formats and workflows
- **Custom Evaluators**: Extensible two-tiered evaluation architecture

See the [functions README](functions/README.md) for detailed evaluation engine documentation.

**Trademarks** 
This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft’s Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). 
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.

AI Model Evaluation Tool Disclaimer

DISCLAIMER: This tool showcases an AI model evaluation and benchmarking tool for healthcare that uses various AI technologies, including foundation models and large language models (such as Azure OpenAI GPT-4). It is not an existing Microsoft product, and Microsoft makes no commitment to build such a product. Generative AI can produce inaccurate or incomplete information. You must thoroughly test and validate that any AI model or evaluation result is suitable for its intended use and identify and mitigate any risks to end users. Carefully review the documentation for every AI tool and service employed.

Microsoft products and services (1) are not designed, intended, or made available as a medical device, and (2) are not designed or intended to replace professional medical advice, diagnosis, treatment, or judgment and should not be used as a substitute for professional medical advice, diagnosis, treatment, or judgment. Customers and partners are responsible for ensuring that their solutions comply with all applicable laws and regulations.



**Data Privacy Disclaimer**

DISCLAIMER: This tool illustrates an AI model evaluation and benchmarking tool for healthcare. It is not an official Microsoft product, and Microsoft makes no commitment to build such a product. All data you supply to this tool is your sole responsibility.

You must ensure that any data used with this tool is PHI-free and fully de-identified or anonymized in accordance with all applicable privacy laws, regulations, and organizational policies (e.g., HIPAA, GDPR, or local equivalents). Do not upload, process, or expose any data that could directly or indirectly identify an individual.

Before using this tool, verify that:
1. The data has been properly de-identified or anonymized.
2. Appropriate consents or legal bases for processing have been obtained where required.
3. You have the legal right, authority, and ownership to use the data, and its use here does not violate any contractual, licensing, or proprietary restrictions.  
4. All downstream uses of the data remain compliant with relevant laws and regulations.

**Microsoft products and services (1) are not designed, intended, or made available as a medical device, and (2) are not designed or intended to replace professional medical advice, diagnosis, treatment, or judgment and should not be used as a substitute for professional medical advice, diagnosis, treatment, or judgment. Customers and partners are responsible for ensuring that their solutions comply with all applicable laws and regulations.**
