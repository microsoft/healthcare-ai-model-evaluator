# Frequently Asked Questions

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

HAIME operates within your security perimeter. All its operation, processing and storage is within this perimeter. The answer depends on your operational parameters. 

Achieving HIPAA compliance requires proper configuration and operational practices by your organization. Consult with your compliance team for guidance.

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
- **End-User Tutorial**: Step-by-step walkthrough for common workflows
- **Project Overview**: High-level architecture and design principles
- **GitHub Repository**: Technical documentation and source code

### How do I report bugs or request features?

Please submit issues through our GitHub repository or contact your system administrator for enterprise deployments. 