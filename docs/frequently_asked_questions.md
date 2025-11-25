# Frequently Asked Questions

## General

### What is Healthcare AI Model Evaluator (HAIME)?

HAIME is an open-source framework designed to help healthcare organizations rigorously evaluate AI models using their own data, clinical tasks, and success metrics. It empowers clinicians and organizations to make evidence-based decisions about AI adoption by providing transparent, customizable, and repeatable evaluation workflows.

### Why is HAIME important for healthcare organizations?

Healthcare AI adoption faces a trust gap due to generic benchmarks and opaque vendor claims. HAIME addresses this by enabling context-specific evaluations that reflect real-world patient populations, workflows, and organizational priorities.

## Key Features

### What are the main capabilities of HAIME?

- No-code evaluation workflows for clinicians without programming expertise
- Flexible dataset management for structured and unstructured data, including imaging
- Customizable clinical task framework with prompt engineering and tailored metrics
- Human-in-the-loop evaluation combining expert judgment with automated metrics
- Universal model integration for any AI endpoint (commercial, open-source, proprietary)
- Analytics and reporting with visualization tools and export options for regulatory needs

### Does HAIME support multimodal evaluation?

Yes. HAIME supports text and image data, enabling comprehensive evaluation across clinical documentation and medical imaging. 

## Deployment & Integration

### How can organizations deploy HAIME?

- Deploy locally to maintain full data control and privacy
- Customize workflows to align with clinical requirements
- Integrate seamlessly with existing AI endpoints and healthcare systems
- Scale strategically across departments and specialties 

### Is HAIME integrated with Microsoft’s ecosystem?

Yes. HAIME complements Microsoft’s healthcare AI offerings, including Azure AI Foundry and Azure Machine Learning, and aligns with industry initiatives like MedHelm and HealthBench. 

## Value Proposition for Teams

### How does HAIME help Sales teams?

It provides a clear differentiation by addressing the trust gap in healthcare AI adoption. Sales can position HAIME as a solution for customers seeking transparency, autonomy, and rigorous evaluation before committing to AI investments. 

### What’s the marketing message?

Empowering confident model selection with a purpose-built evaluation tool for healthcare
To help ensure agentic AI delivers meaningful results in healthcare, organizations should select models that have demonstrated effectiveness for their specific tasks. Healthcare AI model evaluator, available now on GitHub, enables teams to test and validate model performance on relevant clinical tasks using their own data and in their own environment. Its intuitive and flexible interface allows organizations to create custom tests and designate human experts or AI models to evaluate the output. Purpose-built for healthcare needs, the evaluator supports evidence-based model selection to help reduce risk and build trust in AI.

https://youtu.be/u4phpQHFgdM?si=vvPbWsa7rJEVz4oc


### How does HAIME support customer success and IA teams?

By reducing friction in AI evaluation, HAIME eliminates reliance on expensive consultants or custom-built tools, enabling faster AI adoption and continuous performance monitoring. 

## Roadmap & Release

### What is the release timeline?

Ignite 2025: Open-source release / MVP

Further features / improvements will be prioritized based on customer needs & feedback

## Getting Started

### Where can customers access HAIME?

GitHub Repository: https://github.com/microsoft/Healthcare-AI-Model-Evaluator

Demo Requests: hlsfrontierteam@microsoft.com 

### How is HAIME different than Azure AI Foundry evaluation?

https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/evaluate-generative-ai-app?view=foundry-classic

HAIME complements Azure AI Foundry's evaluation capabilities by providing additional customization for healthcare-specific evaluation scenarios:
- Full deployment within customer environments under complete customer control
- Customizable human evaluation workflows and UI/UX for text-based and image-based model outputs
- Support for evaluation of AI models that do not use Microsoft/Azure based or compatible endpoints
- Multi-reviewer consensus workflows: Support for clinical review panels and inter-rater reliability tracking
- Domain-specific metrics: Healthcare-focused evaluation metrics beyond general NLP measures
- Custom evaluation rubrics: Define healthcare-specific scoring criteria aligned with clinical guidelines
- Cost tracking and budgeting: Built-in token usage and cost monitoring across multiple model providers
- Comprehensive data, user, experiment management, and reporting with workflow customization—no coding required
- Complete operational transparency and feature extensibility through extensive import/export capabilities and open-source architecture

HAIME and Azure AI Foundry are designed to work together. We recommend using both platforms in tandem to address your complete evaluation requirements.

As a general guidance:
- Use Azure AI Foundry for rapid prototyping and standard ML metrics; for evaluation and monitoring of models in a production environment
- Use HAIME when you need more detailed clinical validation, full transparency and extensive documentation/logging, or specialized healthcare evaluation workflows in an isolated research or evaluation environment (prior to deploying into production)

# Deployment / Usage

## Data Management

### What data formats does HAIME support?

HAIME currently accepts data in JSONL (JSON Lines) format only. Each line in the JSONL file must contain at least one input field. If you already have model outputs in your dataset, HAIME can use them directly without regeneration. Alternatively, if you've configured model endpoints, HAIME can generate new outputs on demand.

### What if my data is in JSON or CSV format?

You'll need to convert your data to JSONL format. Many scripting languages (Python, Node.js, etc.) have libraries that can easily convert CSV or JSON to JSONL. Each line of your output file should be a valid JSON object.

### How do I include image data in JSONL?

You can embed images using base64 encoding. Here's an example:
```json
{
  "prompt": "Describe this X-ray image",
  "image_file": ["example.jpg"],
  "images": ["data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAA..."]
}
```

> [!NOTE]
> The Base64 prefix (e.g `data:image/jpeg;base64,`) is required.

### Can I have multiple input fields?

Yes. You can define multiple input fields as separate JSON keys in each line of your JSONL file. Just make sure the structure is consistent across all lines. During the data ingestion step, you'll map each field as an input.

### Can I have multiple output fields?

Yes, you can have multiple output fields per JSONL line. Each output corresponds to one model. When setting up your clinical task, you'll assign each output field to its corresponding model endpoint in HAIME.

---

## Model Configuration

### How do I configure model endpoints?

Navigate to the **Models** screen and click **Add Model**. You have two options:

**Option 1: Use Pre-generated Data**  
If your dataset already contains model outputs, you don't need to provide endpoint details or API keys. HAIME will use the model name solely for data association and ranking purposes.

**Option 2: Generate Data**  
If you want HAIME to generate outputs or use models as evaluators (model-as-a-judge), you must provide:
- **API Key**: Your authentication key for the model service
- **Endpoint**: The URL for the model API
- Additional parameters specific to your model provider

After configuration, click **Test Integration** to verify the connection works properly. Contact your administrator if you need assistance with API credentials.

### Which AI model providers are supported?

HAIME supports major AI providers including:
- Azure OpenAI Service
- Azure Function App
- OpenAI API
- Anthropic Claude
- Other OpenAI-compatible endpoints 
- Other (including DeepSeek, Llama,  etc)

As long as a model provider offers a compatible API endpoint, you can integrate it with HAIME.

### What is "model-as-a-judge"?

Model-as-a-judge means using an AI model (rather than human reviewers) to evaluate outputs from other AI models. This is useful for scaling evaluations or conducting preliminary assessments before human review. To use this feature, you must configure a live model endpoint with valid API credentials. You can then create a new "User", and assign "Model reviewer" as reviewer type.

---

## Clinical Tasks

### What is a clinical task?

A clinical task defines what you expect an AI model to do—such as summarization, question answering, clinical reasoning, document generation, or diagnostic support. 

To create a clinical task, you need to:
1. **Define a prompt** that describes the task for the AI model
2. **Select a dataset** containing inputs and (optionally) outputs
3. **Map outputs to models** by either:
   - Selecting "Use Pre-generated Data" if outputs already exist in your dataset
   - Selecting "Generate Data" to have HAIME create outputs using configured model endpoints

### How do I create a clinical task?

Go to the **Clinical Tasks** screen and click **Add Task**. Configure your task by specifying the dataset, model mappings, prompt template, and evaluation metrics. Refer to the End-User Tutorial for detailed step-by-step instructions.

### What should I use as a prompt?

Your prompt should clearly describe what you want the AI model to do. Be specific about:
- The task objective
- Expected output format
- Any constraints or guidelines
- Tone and style preferences

We recommend using an LLM (such as GPT-4) to help generate and refine effective prompts for your specific use case. Make sure your prompt is consistent with the dataset you are using.

---

## Ground Truth and Evaluation Metrics

### How do I set ground truth?

Check the **Set Ground Truth** box in your clinical task configuration, then specify which field in your dataset contains the reference data. This reference data serves as the gold standard for calculating automated evaluation metrics.

### Do I need ground truth?

No, ground truth is optional. Without ground truth:
- HAIME will use scores from human or AI evaluators to calculate results
- You can still perform comparative evaluations between models
- Automated similarity metrics (BERT score, F1, ROUGE, etc.) won't be available

### What evaluation metrics are available?

HAIME supports several categories of metrics:

**Automated Metrics** (require ground truth):
- BERT Score
- F1 Score
- ROUGE (ROUGE-1, ROUGE-2, ROUGE-L)
- BLEU Score
- Exact Match

**Human Evaluation Metrics**:
- Custom rating scales (1-5, binary, etc.)
- Free-text feedback
- Comparative rankings

**Custom Metrics**:
You can define your own evaluation criteria and scoring rubrics tailored to your specific clinical needs.

---

## Experiments and Assignments

### What is an experiment?

An experiment defines how you'll evaluate one or more clinical tasks. When creating an experiment, you:
1. Select the clinical task(s) to evaluate
2. Write reviewer instructions
3. Define evaluation questions with response options and scoring
4. Specify whether reviewers can edit model outputs
5. Choose the experiment type (single evaluation, A/B testing, etc.)

The questions and scoring you define will be presented to evaluators, and their responses will be used to calculate evaluation scores.

### What is an assignment?

An assignment connects an experiment with specific evaluators (human reviewers or AI models). Once you create an assignment:
1. Select the experiment to run
2. Choose which evaluators should participate
3. Click **Prepare** to generate evaluation trials (this may take time if outputs need to be generated)
4. Click **Run** to launch the assignment

Assigned evaluators will then see their evaluation tasks in the Arena screen.

---

## User Roles and Permissions

### What user roles does HAIME support?

HAIME supports multiple user roles:

**Administrator**: Full access to configure datasets, models, tasks, experiments, and user management

**Reviewer**: Can complete assigned evaluations in the Arena screen

(future) **Data Scientist**: Can create and manage datasets, models, and clinical tasks

(future) **Observer**: Read-only access to view experiments and results

### How do I add reviewers?

Navigate to the **Users** screen, click **Add User**, and configure:
- Name and email address
- Role (check "Reviewer")
- Clinical expertise area
- Reviewer type (Human Reviewer or Model Reviewer)

---

## Cost Tracking

### How does HAIME track costs?

When you configure a model endpoint, you specify the cost per input token and cost per output token. HAIME automatically tracks:
- Total tokens consumed per model
- Cost per evaluation task
- Cumulative costs across experiments
- Cost comparisons between different models

This helps you understand the economic trade-offs between different AI models and evaluation approaches.

### Where can I view cost information?

Cost data is available in the **Rankgings / Metrics Dashboard** and can be filtered by model, task, or experiment. You can also export this data for financial reporting or budget planning.

---

## Reporting and Export

### What reporting features are available?

HAIME provides comprehensive reporting through the **Rankgings / Metrics Dashboard**:
- Performance by model, task, or evaluator
- Cost analysis and comparisons
- Custom metric visualizations

### Can I export evaluation results?

Yes, you can export evaluation data in JSON format for programmatic access.

Exported data includes all evaluation scores, reviewer feedback and metadata.

---

## Security and Privacy

### Where is my data stored?

HAIME is designed for deployment within your own secure infrastructure. All patient data, evaluation results, and model outputs remain under your control within your security perimeter.

### Is HAIME HIPAA compliant?

HAIME operates entirely within your security perimeter, with all operations, processing, and storage under your control. However, HIPAA compliance depends on your specific deployment configuration and operational practices.

Since HAIME can integrate with external model endpoints that may not be HIPAA-compliant, we cannot guarantee end-to-end HIPAA compliance. We strongly recommend using only de-identified, PHI-free data for which you have appropriate usage rights and licenses.

Consult with your compliance and legal teams to ensure your HAIME deployment meets all applicable regulatory requirements.
---

## Scalability

### How many evaluators can participate in an experiment?

There's no hard limit on the number of evaluators. HAIME can scale to accommodate evaluation teams ranging from a single clinical expert to large multi-site reviewer pools.

### How many datasets or models can I manage?

HAIME is designed to handle multiple datasets, models, and concurrent experiments. Actual limits depend on your deployment infrastructure, but the platform is built to scale with your evaluation needs.

### Can I run multiple experiments simultaneously?

Yes, you can run multiple experiments and assignments concurrently. This allows different teams to conduct independent evaluations or compare different evaluation methodologies in parallel.

---

## Troubleshooting

### My model endpoint test is failing. What should I check?

Common issues include:
- **Incorrect API key or endpoint URL**: Verify credentials with your model provider
- **Network connectivity**: Ensure your deployment can reach the model API endpoint
- **Incorrect parameters**: Check that deployment name, API version, and other parameters match your model service configuration
- **Rate limits**: Some providers have rate limits that may cause connection failures

Contact your administrator if you continue experiencing issues.

### Data ingestion failed. What went wrong?

Check that:
- Your file is valid JSONL format (one JSON object per line)
- Each line contains the required input fields
- The file encoding is UTF-8
- Field names are consistent across all lines

### Outputs aren't generating. What should I do?

Verify that:
- Your model endpoint is configured correctly and passes the integration test
- You selected "Generate Data" (not "Use Pre-generated Data") in your clinical task
- The model has access to the input fields specified in your prompt
- You're not hitting API rate limits or quota restrictions

---

## Getting Help

### Where can I find more documentation?

Refer to:
- **Deployment**: https://github.com/microsoft/healthcare-ai-model-evaluator/blob/main/DEPLOYMENT.md
- **End-User Tutorial**: Step-by-step walkthrough for common workflows https://github.com/microsoft/healthcare-ai-model-evaluator/blob/main/docs/getting_started_end_user_tutorial.md
- **Project Overview**: High-level architecture and design principles https://github.com/microsoft/healthcare-ai-model-evaluator/blob/main/docs/project_overview.md
- **GitHub Repository**: Technical documentation and source code https://github.com/microsoft/healthcare-ai-model-evaluator

### How do I report bugs or request features?

Please submit issues through our GitHub repository or contact your system administrator for enterprise deployments. 