# Healthcare AI Model Evaluator

Healthcare AI Model Evaluator is a medical AI model benchmarking platform with integrated evaluation engine to assist multi-disciplinary healthcare teams build and validate AI systems.

## Solution and Features Overview

![High-level diagram showing main workflow of Healthcare AI Model Evaluator](./docs/images/haime_solution.png)


The Healthcare AI Model Evaluator enables seamless collaboration between data scientists and clinical researchers, providing tools for both technical model development and expert medical validation:

- **Arena Interface**: Intuitive web-based UI for model comparison and validation, designed for clinical teams without requiring deep technical expertise
- **Expert Review Workflows**: Enable medical professionals to systematically validate model outputs with customizable evaluation criteria
- **Multi-Reviewer Support**: Combine evaluations from multiple human experts and AI reviewers for comprehensive assessment
- **Model-as-Judge**: LLM-based evaluation via Azure OpenAI for subjective metrics and complex assessments
- **Built-in Metrics**: Automated computation including exact match, ROUGE, BERTScore, TBFact factual consistency, and Elo rankings
- **Custom Evaluators**: Extensible add-on architecture for domain-specific metrics with transparent intermediate steps
- **Built-in Model Connectors**: Direct integration with Azure OpenAI, AI Foundry Model Catalogue, Azure ML endpoints, and custom REST APIs
- **Multimodal Support**: Handle text, images, and multimodal inputs across different model types
- **Data Privacy**: Fully deployed in your Azure subscription. You maintain complete control over your data and models

For a complete overview of the Healthcare AI Model Evaluator platform, refer to the [Project Overview](./docs/project_overview.md). And explore the Getting Started guides to familiarize with the UI and relevant use-cases:
- [End user guide](./docs/getting_started_end_user_tutorial.md)
- [Zero-Shot Classification Validation](./docs/getting_started_zero_shot_classification_workflow.md)

## Deployment

> [!IMPORTANT]
> See [DEPLOYMENT.md](./DEPLOYMENT.md) for complete deployment guide, configuration options, and troubleshooting.

### Quick start

Deploy the complete Healthcare AI Model Evaluator platform with a single command using Azure Developer CLI (azd).

```
azd up
```

## Local Development

### Frontend setup
```bash
cd frontend
npm install
npm run dev
```

### Backend Setup
```bash
export AZURE_STORAGE_CONNECTION_STRING=[Your Storage Account connection string]
export COSMOSDB_CONNECTION_STRING=[Your mongodb connection string]

cd backend
dotnet restore
dotnet run --project src/MedBench.API/MedBench.API.csproj
```

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
- **Hosting**: Container Apps + Azure Functions
- **Database**: Azure Cosmos DB (MongoDB API, serverless)
- **Storage**: Shared Azure Blob Storage with function triggers
- **AI Services**: Azure OpenAI for LLM-based evaluation
- **Security**: Key Vault + Managed Identity throughout
- **Monitoring**: Log Analytics + Application Insights

## Integration with External Frameworks

Healthcare AI Model Evaluator integrates with external evaluation frameworks:
- **MedHelm**: Data conversion utilities in `functions/notebooks/`
- **Custom Evaluators**: Extensible two-tiered evaluation architecture

See the [functions README](functions/README.md) for detailed evaluation engine documentation.

**Trademarks** 
This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft’s Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). 
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.

AI Model Evaluation Tool Disclaimer

DISCLAIMER: This tool showcases an AI model evaluation and benchmarking tool for healthcare that uses various AI technologies, including foundation models and large language models (such as Azure OpenAI GPT-4). It is not an existing Microsoft product, and Microsoft makes no commitment to build such a product. Generative AI can produce inaccurate or incomplete information. You must thoroughly test and validate that any AI model or evaluation result is suitable for its intended use and identify and mitigate any risks to end users. Carefully review the documentation for every AI tool and service employed.

This tool allows the same configured models to be used in both output generation and output evaluation.  However, it is generally not the best practice to use a model to evaluate the output of that same model (e.g., don't use GPT-4.1 as a judge to evaluate GPT-4.1) as this might lead to heavily skewed results.

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
