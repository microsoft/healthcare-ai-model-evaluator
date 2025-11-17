# Zero-Shot Classification Workflow with Healthcare AI Model Evaluator (HAIME)

## Overview

In healthcare AI applications, the stakes are high. When models generate X-ray reports, analyze medical images, or suggest drug interactions, clinical experts need to validate outputs before they can be trusted in practice. Even when ground truth labels exist, establishing confidence in model performance requires systematic evaluation by domain experts who can identify edge cases, assess clinical relevance, and catch subtle errors that automated metrics might miss. This is especially critical for complex tasks like medical image interpretation, where models may perform well on aggregate metrics but fail on specific patient subgroups or rare pathologies. Human-in-the-loop validation is closer to clinical reality, and enable teams to build trust, identify model limitations, and gather the high-quality annotations needed for continuous improvement.

The Healthcare AI Model Evaluator (HAIME) is designed to streamline this validation process. It enables clinical teams to systematically review model outputs, combine evaluations from multiple experts (including AI reviewers like LLMs), compute transparent performance metrics, and export curated annotations for downstream training workflows. By integrating human expertise into the evaluation pipeline, HAIME helps teams move from experimental models to clinically validated systems with confidence.

This guide demonstrates how to leverage the Healthcare AI Model Evaluator (HAIME) platform for analysing and validating results from the [MedImageInsight for Zero-Shot Classification example notebook](https://github.com/microsoft/healthcareai-examples/blob/6946fe6ef02ab69dc7f1387e899d13fe96dbcb9a/azureml/medimageinsight/zero-shot-classification.ipynb). In this guide you will learn how to use HAIME for:

- **Upload datasets and connect to models**.
- **Multi-reviewer evaluation**: enable _human-in-the-loop validation_ by collecting feedback from Subject Matter Experts, and combine expert evaluation with _Model reviewers_ for comprehensive assessment.
- **Systematic evaluation**: Compute performance metrics and compare multiple approaches, with support for _custom metrics_ that match your use-case.
- **Bridge to improvement**: Export annotations for model training pipelines and further analysis.

**The Zero-Shot Classification notebook** explores using MedImageInsight, a foundational medical imaging model, to classify 2D chest X-rays into five pathology categories using a zero-shot approach. The notebook covers:

1. Generating image embeddings from chest X-rays using MedImageInsight
2. Generating text embeddings from pathology class descriptions (e.g., "x-ray chest anteroposterior Cardiomegaly")
3. Calculating similarity (dot product) between image and text embeddings
4. Applying softmax to obtain class probabilities and selecting the highest as prediction

---

## Getting Started with Existing Predictions

This workflow leverages predictions already generated from the lab notebook (`zero_shot_classification_results.csv`), allowing you to explore HAIME's evaluation capabilities immediately.

### Prerequisites

- Deployed HAIME instance. For detailed instructions follow the [deployment guide](../DEPLOYMENT.md).
- Access credentials to the HAIME platform.
- To get the necessary data and results for this guide, follow the Prerequisites section of the [Zero-Shot Classification notebook](https://github.com/microsoft/healthcareai-examples/blob/6946fe6ef02ab69dc7f1387e899d13fe96dbcb9a/azureml/medimageinsight/zero-shot-classification.ipynb). You will need:
   - Zero-shot classification results: `zero_shot_classification_results.csv`
   - Ground truth labels CSV: `dcm_sample_label.csv`
   - DCM images used in the notebook.

### Step 1: Prepare Data for HAIME

The lab notebook generates predictions in CSV format. We need to transform this into JSONL (JSON Lines) format where each line represents one data sample.

**Input files**:
- `zero_shot_classification_results.csv` - contains predictions
- `dcm_sample_label.csv` - contains ground truth labels
- DICOM image files

**Required transformation**:

Create a Python script to convert the CSV to JSONL format with base64-encoded images. To run the script below install the required dependencies `pip install pandas pydicom pillow numpy pylibjpeg pylibjpeg-libjpeg`

```python
import pandas as pd
import json
import os
import base64
import pydicom
from PIL import Image
import numpy as np
from io import BytesIO


def convert_dcm_to_jpg_base64(dcm_path: str, quality: int = 85) -> str:
    """
    Convert a DICOM file to a base64-encoded JPG image.
    
    Args:
        dcm_path: Path to the DICOM file
        quality: JPG quality (1-100, default 85)
    
    Returns:
        Base64-encoded JPG image string
    """
    # Load DICOM file
    dcm = pydicom.dcmread(dcm_path)
    pixel_array = dcm.pixel_array
    
    # Normalize to 0-255 range
    pixel_array = pixel_array - np.min(pixel_array)
    pixel_array = pixel_array / np.max(pixel_array) * 255.0
    pixel_array = pixel_array.astype(np.uint8)
    
    # Convert to PIL Image
    image = Image.fromarray(pixel_array)
    
    # Convert to JPG in memory
    buffer = BytesIO()
    image.save(buffer, format='JPEG', quality=quality, optimize=True)
    image_bytes = buffer.getvalue()
    
    # Encode as base64
    return "data:image/jpeg;base64," + base64.b64encode(image_bytes).decode('utf-8')


# Update these as needed
BASE_DATA_PATH="path/to/data/files"
IMAGES_PATH=os.path.join(BASE_DATA_PATH, "images")
RESULTS_PATH=os.path.join(BASE_DATA_PATH, "csv", "zero_shot_classification_results.csv")
DCM_SAMPLE_LABELS_PATH=os.path.join(BASE_DATA_PATH, "csv", "dcm_sample_label.csv")

# Load the prediction results
results_df = pd.read_csv(RESULTS_PATH)

# Load ground truth labels
labels_df = pd.read_csv(DCM_SAMPLE_LABELS_PATH)

# Merge if needed
merged_df = results_df.merge(labels_df, left_on='file_name', right_on='Name', how='left')

# Convert to JSONL format
with open('haime_dataset.jsonl', 'w') as f:
    for _, row in merged_df.iterrows():
        # Convert DICOM to JPG and encode as base64
        image_path = os.path.join(IMAGES_PATH, row['file_name'])
        image_base64 = convert_dcm_to_jpg_base64(image_path)
        
        record = {
            "image": image_base64,  # Base64-encoded JPG image
            "categories": [
                "x-ray chest anteroposterior No Finding",
                "x-ray chest anteroposterior Support Devices",
                "x-ray chest anteroposterior Pleural Effusion",
                "x-ray chest anteroposterior Cardiomegaly",
                "x-ray chest anteroposterior Atelectasis"
            ],
            "MedImageInsight-ZeroShot-Baseline": row['zero_shot_pred'],
            "ground_truth": row['ground_truth_label'],
            # Optional: Include metadata
            "metadata": {
                "file_name": row['file_name'],
                "label_numeric": row.get('Label', None)
            }
        }
        f.write(json.dumps(record) + '\n')
```

> [!IMPORTANT]
> When uploading images as Base64, don't forget the prefix (e.g `data:image/jpeg;base64,`)

> [!TIP]
> HAIME supports nested JSON structures, allowing you to organize metadata, model outputs, or other information in hierarchical formats within your JSONL records.

**Expected JSONL structure**:
```json
{"image": "base64_encoded_image_data...", "categories": ["x-ray chest anteroposterior No Finding", "x-ray chest anteroposterior Support Devices", "x-ray chest anteroposterior Pleural Effusion", "x-ray chest anteroposterior Cardiomegaly", "x-ray chest anteroposterior Atelectasis"], "MedImageInsight-ZeroShot-Baseline": "x-ray chest anteroposterior No Finding", "ground_truth": "No Finding", "metadata": {"file_name": "1.3.6.1.4.1.55648.013051327602219610100989737191708734008.2.1.green.dcm", "label_numeric": 0}}
```

### Step 2: Create Dataset

1. Navigate to **Datasets** in HAIME
2. Click **"Add Dataset"**
3. Provide dataset details:
   - **Name**: `chest-xray-zero-shot-classification`
   - **Origin**: Zero-Shot Demo
   - **Description**: Zero-shot classification results from MedImageInsight on chest X-rays
   - **AI Model Type**: Image Classification
4. Upload the JSONL file created in Step 1
5. Configure input and output fields:
   - **Input fields**: 
     - `image` (the base64-encoded image), with Input Data Type `Image URL`
     - `categories` (the list of possible classes), and add the whole array as `Text`.
   - **Output fields**: 
     - `MedImageInsight-ZeroShot-Baseline` (the model prediction), as `Text`
     - `ground_truth` (the correct label), as `Text`.
6. Click **"Save"**

![Screenshot of Create Dataset page](/docs/screen-shots/zero_shot_create_dataset.png)

### Step 3: Create Model

1. Navigate to **Models** → **"Add Model"**
2. Configure model details:
   - **Model Name**: `MedImageInsight-ZeroShot-Baseline`
   - **Model Type**: Image to Text
   - **Origin**: Microsoft
3. Click **"Save"**

> [!TIP]
> At this point, you could also connect this model entry to a deployed model endpoint to generate outputs directly through the platform. This requires:
> - Deploying your MedImageInsight zero-shot classifier as an API endpoint (e.g., Azure ML Online Endpoint)
> - Implementing a model connector that adapts your endpoint's input/output format
> - Configuring authentication and request parameters
>
> Depending on your model's expected input format, you may need to implement custom model connectors.

### Step 3.1 (Optional): Add LLM as Model Reviewer

To include an AI reviewer alongside human experts:

1. **Deploy LLM in Azure OpenAI** (e.g. GPT-5)
2. Click **"Add Model"**
3. Configure model details:
   - **Model Name**: `openai/gpt-5` (or your chosen LLM)
   - **Model Type**: Multimodal
   - **Origin**: OpenAI
   - For **Integration Type** select `OpenAI Reasoning Model`
   - Add the required **Parameters**:
      - `DEPLOYMENT` (deployment name)
      - `ENDPOINT`
      - `API_KEY`
4. Add Model as a Reviewer:
   - Navigate to **"User Management"** → **"New User"**
   - Give it a name and email (email is required, but not used for models).
   - For **Reviewer Type** select **"Model Reviewer"**, and assign the model you just created.

This LLM can later be assigned as an additional reviewer in the assignment step

![Screenshot of Create Model page](/docs/screen-shots/zero_shot_create_model_llm.png)

### Step 4: Create Clinical Task


1. Navigate to **Clinical Tasks** → **"Create Task"**
2. Configure task settings:
   - **Name**: `Chest X-Ray Pathology Classification Review`
   - Add the dataset ground truth:
      - For **Dataset**, select the one created in [Step 2](#step-2-create-dataset).
      - Check the **Set ground truth** checkbox, and under **Uploaded Output Index** select the `ground_truth` field.
      - Click **"Add Dataset-Model Pair**"
   - Add the model output:
      - Fill the **Dataset** field again with the one created in [Step 2](#step-2-create-dataset).
      - For **Model**, select the one created in [Step 3](#step-3-create-model)
      - Under **Output Type**, select **"Use Pregenerated Data"**, and under **Uploaded Output Index** select the `MedImageInsight-ZeroShot-Baseline` field.
      - Click **"Add Dataset-Model Pair**"
   - **Evaluation Metric**, set **"Accuracy metrics"**
   - (Optional) If you have access to a live deployment of the MedImageInsight Zero-Shot classifier, or if you want to compare your pre-generated results against results from an LLM (e.g GPT-5), you may select **"Generate Data"** as **Output Type**.
      - Once the Clinical Task is created, you can select it, and click **"Generate Outputs"** from the top menu.

> [!TIP]
> A Clinical Task can be composed of outputs from different datasets, each with their own ground truth.

> [!NOTE]
> If you have access to a live deployment of the MedImageInsight Zero-Shot classifier, or if you want to compare your pre-generated results against results from an LLM (e.g GPT-5), you may select **"Generate Data"** as **Output Type**.
>
> Once the Clinical Task is created, you can select it, and click **"Generate Outputs"** from the top menu.

![Screenshot of Create Clinical Task page](/docs/screen-shots/zero_shot_create_clinical_task.png)

### Step 5: Create Experiments

1. Navigate to **Experiments** → **"New"**
2. Configure experiment:
   - **Experiment Name**: `MedImageInsight Zero-Shot Baseline Evaluation`
   - **Clinical Task**: Select `Chest X-Ray Pathology Classification Review`
   - **Models**: Check `MedImageInsight-ZeroShot-Baseline`
      - If you generated outputs for another model, you may select them here too.
   - What you add under **Reviewer Instructions** will be shown to reviewers in the Arena UI.
   - **Allow Output Editing** is useful for generative AI outputs, with long text or image bounding boxes.
   - **Experiment Type** defines what reviewers will do:
      - _Preference Assessment_: A/B test between different model outputs.
      - _Single Evaluation_: question based evaluation (thumbs up and down, likert score...).
         - You may add extra **Experiment Questions**, to get annotations for specific criteria. Again, that is especially useful for more complex evaluation tasks.

> [!TIP]
> When adding additional **Experiment Questions**, they will be saved as a _New Metric_ for future experiments. Questions' answers are customizable, you can set the _Option Text_ (what the reviewer sees), and _Option Value_ (internal value of that answer, e.g. `Bad` is 1 and `Great` is 5).

![Screenshot of Experiment creation with multiple models](/docs/screen-shots/zero_shot_create_experiment.png)

### Step 6: Create Assignments

1. Navigate to **Assignments** → **"New Assignment"**
2. Configure assignment:
   - **Name**: `MedImageInsight Zero-Shot Baseline Evaluation`
   - **Experiment**: Select the experiment created in the previous step
   - **Reviewers**: Add your user and optionally the user created for in [Step 3.1](#step-31-optional-add-llm-as-model-reviewer)
3. Select the assignment you created, click **Prepare**, and then **Run**.
   - This will trigger requests to the model-as-judge, if you added a model as reviewer.
4. Navigate to **"Arena"** in the top down menu, and you should see the new assignment.

> [!TIP]
> Select the assignment you created and click on **"Explore"** to visualize reviewers' evaluations, including results from the model reviewer.

### Step 7: Working with results

Finally, when annotations are done, you may navigate to **"Rankings"** in the top menu, and select your clinical task from the list. There you should see results for the metrics created in your experiment, based on the results given by the annotators. 

Alternatively, you can export the evaluation results (**"Assignment"** → **"Export Data"**). This enables you to perform your own downstream analysis or training pipelines. The exported data is also useful for experimenting with custom evaluators (e.g model-as-judge) before they can be deployed and connected to your existing Healthcare AI Model Evaluator instance.

> [!TIP]
> If you have outputs for multiple models, you could now experiment setting up a _Preference Assessment_ experiment, to compare `MedImageInsight-ZeroShot-Baseline` with an LLM of your choice. These results will feed into the Elo Score, displayed in the Rankings page.

## Closing Thoughts

This evaluation-to-improvement cycle is at the heart of what is enabled by the Healthcare AI Model Evaluator. In this guide we learned how to load datasets and models into HAIME, and we've enabled systematic evaluation with human expert and model reviewers, which can feed further model improvement pipelines.

### Scaling Your Evaluation Workflow

The workflow you've just completed can be extended in several ways:

**Compare multiple models**: Add outputs from different models (e.g., an LLM-based classifier alongside MedImageInsight) and use _Preference Assessment_ experiments to run A/B comparisons. These results feed into Elo rankings, giving you a systematic way to compare model performance across your entire evaluation set.

**Track performance over time**: As you iterate on your models, create new experiments with the same clinical task and reviewers. This lets you measure improvements on the same challenging cases, with consistent evaluation criteria and transparent metrics tracking progress across iterations.

**Define custom evaluation criteria**: You can add domain-specific evaluation questions, or implement your own custom evaluators.