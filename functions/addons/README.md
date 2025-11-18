# Healthcare AI Model Evaluator Add-ons

This directory contains add-on functionality that extends the core Healthcare AI Model Evaluator evaluation engine with specialized metrics and evaluation capabilities.

## Overview

Healthcare AI Model Evaluator is a medical AI model benchmarking platform. Part of it is an Evaluator Engine that handles standard metrics (exact match, image similarity, text summarization), add-ons provide specialized evaluation capabilities for domain-specific use cases.

### Core Architecture

- **Healthcare AI Model Evaluator Engine**: Core evaluation infrastructure with standard metrics
- **Arena Integration**: Model comparison and ranking platform
- **Add-ons**: Specialized evaluators for custom metrics and domain-specific assessments

## Available Add-ons

### Function App Runner Integration

The AzureFunctionAppRunner allows Healthcare AI Model Evaluator to use Azure Function Apps as model runners that generate outputs through external processing. Unlike traditional evaluators that assess existing outputs, function app runners act as external models that can perform sophisticated processing, AI inference, or other computational tasks to generate responses.

**Key Features:**
- **Model Runner**: Generates model outputs (not just evaluation metrics)
- **Multimodal Support**: Processes complex inputs including text and images
- **Flexible Processing**: Supports any experiment type (Simple Evaluation, Arena, etc.)
- **External Integration**: Allows integration with external AI services and processing pipelines
- **Prompt Separation**: Handles base prompts and output instructions separately for better prompt engineering

**Input Schema:**
Function apps receive a JSON blob containing:
- `model_run`: Complete model run data with dataset instances and existing results
- `function_type`: Type identifier ("evaluator")
- `job_id`: Unique identifier for tracking
- `base_prompt`: Evaluation context and scenario instructions
- `output_instructions`: Experiment-specific instructions (e.g., "Rate 1-5" or "Choose A or B")

**Output Schema:**
Function apps must return either:
- Success: `{"output": "Generated model output text"}`
- Error: `{"error": "Error message describing what went wrong"}`

**Processing Flow:**
1. Input uploaded to blob container (`evaluatorjobs`)
2. Azure Function App processes input and generates output
3. Results retrieved from output container (`evaluatorresults`)
4. Output extracted and returned as model's generated content

**Configuration:**
- `FunctionAppType`: Function app type ("evaluator")
- `TimeoutSeconds`: Maximum wait time (default: 300)
- `StorageConnectionString`: Azure Storage connection (default to storage account connected to the backend)
- `ClinicalTaskId`: Optional task ID for dataset naming

**Use Cases:**
- External AI model integration
- Complex multimodal processing
- Custom inference pipelines
- Specialized domain processing

### Summary Evaluator (`evaluator/`)

A novel question-based summarization evaluator that uses Healthcare AI Model Evaluator's `SummaryEvaluatorRunner` implementation for medical text summarization assessment. This evaluator implements a multi-stage evaluation process designed to reduce self-bias in model-as-judge approaches.

**Evaluation Process:**
1. **Question Generation**: Analyzes the original input and generates comprehensive questions that a thorough summary should answer, along with importance ratings (1-3) and expected answers
2. **Summary-based Answering**: Attempts to answer the generated questions using only information from the AI-generated summary, providing completeness ratings (1-5) and identifying missing information
3. **Objective Scoring**: Evaluates overall summary quality (1-5) based on the generated questions, expected vs. actual answers, and evaluation criteria including groundedness, completeness, relevance, and fluency

**Key Features:**
- Novel approach to reduce evaluator self-bias by having the LLM generate its own evaluation criteria
- Monitors `evaluatorjobs/` container (separate from main metrics)
- Only processes jobs where `metrics_type` is `"summarization"`
- Azure OpenAI integration for LLM-based evaluation
- Question caching between evaluations for consistency
- Detailed intermediate artifacts (questions, answers, ratings) for human review

**Approach Benefits:**
- **Reduced Self-Bias**: The evaluator generates the questions it will use for assessment, creating more consistent evaluation criteria
- **Transparency**: All intermediate steps (questions, answers, ratings) are preserved for analysis
- **Medical Domain Focus**: Specialized for medical summarization tasks with domain-specific evaluation guidelines

**Limitations & Cautions:**
- **Experimental Approach**: This is a novel evaluation methodology that hasn't been extensively tested in real-world scenarios
- **LLM Dependency**: Evaluation quality heavily depends on the underlying LLM's capabilities and domain knowledge
- **Specialized Knowledge**: May fall short on highly specialized datasets requiring deep domain expertise beyond the LLM's training

**Input Format:**
- Processes the same input format as the main metrics function app
- Only evaluates jobs where `metrics_type` is `"summarization"`
- Extracts reference and generated text from ModelRun structure

**Deployment:**
- Includes deployment script that packages MedBench + Addon
- Supports zip deployment with bundled dependencies
- Ready for future migration when MedBench becomes a PyPI package

**Use Cases:**
- Medical report summarization validation
- Clinical note summary assessment
- Patient summary quality evaluation
- Research article abstract evaluation

## Creating New Add-ons

Add-ons follow a standardized pattern for easy integration and can serve two primary purposes:

### Add-on Types

**Function App Runners**: Act as external model runners that generate outputs through Azure Function Apps
- Generate model outputs for any experiment type
- Process multimodal inputs (text + images)
- Integrate with external AI services or processing pipelines
- Return generated content for further system processing

**Evaluators**: Assess and score existing model outputs with specialized metrics
- Monitor dedicated containers for evaluation jobs
- Apply domain-specific evaluation criteria
- Generate detailed assessment reports
- Provide specialized metrics beyond standard exact match/similarity

### Directory Structure
```
addons/
├── your-addon/
│   ├── function_app.py          # Azure Function entry point
│   ├── host.json               # Function app configuration
│   ├── requirements.txt        # Python dependencies
│   ├── evaluator.py           # Core evaluation logic
│   └── local.settings.json    # Local development settings
└── README.md                   # This file
```

### Infrastructure
```
infra/
└── addons/
    └── your-addon.bicep        # Azure infrastructure template
```

### Integration Points

**For Function App Runners:**
1. **Blob Storage**: Input containers (`evaluatorjobs`) for job processing, output containers (`evaluatorresults`) for results
2. **JSON Schema**: Standardized input with `model_run`, `base_prompt`, `output_instructions`, and metadata
3. **Output Format**: Simple JSON response with `output` field for success or `error` field for failures
4. **Timeout Handling**: Configurable timeout periods for external processing
5. **Error Management**: Graceful handling of non-JSON responses and Azure storage exceptions

**For Evaluators:**
1. **Blob Triggers**: Monitor dedicated input containers (e.g., `evaluatorjobs/` for the Summary Evaluator)
2. **Standardized Output**: Write results to dedicated output containers with naming pattern `{job-id}-{addon-name}-results.json`
3. **Health Checks**: Implement `/api/health` endpoint for monitoring
4. **Configuration**: Use environment variables for external service configuration

## Deployment

For deployment please refer to the [deployment-guide](../docs/DEPLOYMENT_GUIDE.md).

## Integration with Arena

Add-ons seamlessly integrate with the Arena platform by:

1. **Shared Data Format**: Using standardized JSON schemas for inputs/outputs
2. **Unified Storage**: Writing results to the same storage containers
3. **Consistent Metadata**: Including evaluation type, timestamps, and request tracking
4. **Pattern-based Discovery**: Arena can discover results using file naming patterns

**Function App Runners** integrate as external model runners that generate outputs which can then be evaluated using standard or specialized metrics. **Evaluators** provide specialized assessment capabilities that complement the core evaluation engine.

This allows Arena to aggregate results from multiple model runners and evaluators, providing comprehensive model assessments across standard and specialized metrics while supporting external processing capabilities.
