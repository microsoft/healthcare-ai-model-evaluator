# Healthcare AI Model Evaluator (HAIME) End-User Tutorial to get started

This tutorial provides step-by-step instructions for setting up and conducting a healthcare Q&A evaluation experiment using HAIME. You will learn how to import datasets, configure models, create clinical tasks, design experiments, and assign evaluation tasks to reviewers.

---

## Prerequisites

- Access to HAIME platform
- Azure OpenAI API credentials (or alternative model endpoint)
- Sample dataset (JSONL format)

---

## Step 1: Download a Dataset

Download a sample healthcare Q&A dataset in JSONL format from Hugging Face:

```
https://huggingface.co/datasets/adrianf12/healthcare-qa-dataset-jsonl/resolve/main/healthcare_qa_dataset.jsonl?download=true
```
or you can use your own dataset, as long as it is in jsonl format and has at least one input and one output node per line.

Save the file to your local machine for use in the next step.

---

## Step 2: Ingest the Dataset

1. Navigate to **Data Management** in the HAIME interface

![alt text](/docs/screen-shots/dataset.png)

2. Click **Add Dataset**
3. Enter the dataset information:
   - Provide a descriptive name for your dataset
   - Add relevant metadata (optional)
4. Click **Select jsonl file** and browse to the downloaded dataset file
5. Configure input mapping:
   - Click the dropdown for **Select an Input Data Key Path**
   - Select `prompt` as the input field
   - Click **Add Input Mapping**
6. Configure output mapping:
   - Click the dropdown for **Select an Output Data Key Path**
   - Select `completion` as the output field
   - Click **Add Output Mapping**
7. Click **Save** to complete the dataset ingestion

---

## Step 3: Configure a Model Endpoint

**Note:** You may skip this step if you have previously configured model endpoints.

This example demonstrates configuration using GPT-4o mini from the Azure AI Foundry model catalog.
1. Navigate to the **Models** screen
2. Click **Add Model** in the upper left corner

![alt text](/docs/screen-shots/add-model.png)

3. Configure the model settings in the right panel:
   - **Name:** `openai/gpt-4o-mini`
   - **Model Type:** Select the appropriate type from the dropdown
   - **Origin:** OpenAI
   - **Cost Per Token In:** `7.5e-8` (or decimal)
   - **Cost Per Token Out:** `3e-7` (or decimal)
   - **Description:** (optional)
4. Configure model parameters:
   - **DEPLOYMENT:** `<your-gpt4o-mini-deployment-name>`
   - **ENDPOINT:** `https://<your-tenant-prefix>.openai.azure.com`
   - **API_KEY:** `<your-api-key>`
   - **Integration Type:** Select `OpenAI` from the dropdown
5. Click **Save** to store the model configuration

---

## Step 4: Create a Clinical Task
1. Navigate to the **Clinical Tasks** screen
2. Click **Add Task** in the upper left corner

![alt text](/docs/screen-shots/task.png)

3. Configure the task settings in the right panel:
   - **Name:** `Open-ended Healthcare Q&A`
   - **Dataset Model Pairs:** Select the dataset you created in Step 2
   - **Ground Truth Configuration:**
     - Check the **Ground Truth** box if you want to use the completion data from the dataset as a basis for calculating metrics
     - If enabled, select `Completion` as the ground truth field
   - **Model:** Select the model you configured in Step 3
   - **Output Type:** Select `Generate Data` to use the model endpoint for generating responses
   - **Prompt Template:** Enter the following prompt:
     ```
     You are a knowledgeable healthcare assistant. Answer the following question clearly, accurately, and in a way that is easy to understand for a general audience. Include:
     - A concise explanation of the topic
     - Key facts or steps if applicable
     - Any important precautions or disclaimers
     
     Question: {input}
     ```
   - **Evaluation Metric:** Select `Text-based metrics`
4. Click **Save** to create the clinical task

---

## Step 5: Create an Experiment
1. Navigate to the **Experiments** screen
2. Click **Add Experiment** in the upper left corner

![alt text](/docs/screen-shots/add-experiment.png)

3. Configure the experiment settings:
   - **Name:** `Healthcare Open-ended Q&A`
   - **Clinical Task:** Select the clinical task created in Step 4
   - **Description:** `Evaluate open-ended answers to healthcare questions`
   - **Reviewer Instructions:** `Please read the healthcare question (left) and answer the questions about the model output (below).`
   - **Allow Output Editing:** Yes
   - **Experiment Type:** `Single Evaluation`
4. Define evaluation questions:
   - Click **Add Question** to create a new evaluation criterion
   - **Question 1 - Clinical Accuracy:**
     - **Metric Name:** `Clinical Accuracy`
     - **Question Text:** `Is the information factually correct and aligned with current clinical guidelines?`
     - **Response Options:**
       - Option 1: Text: `1 - Poor`, Value: `1`
       - Option 2: Text: `2 - Fair`, Value: `2`
       - Option 3: Text: `3 - Good`, Value: `3`
       - Option 4: Text: `4 - Very Good`, Value: `4`
       - Option 5: Text: `5 - Excellent`, Value: `5`
5. Click **Save** to create the experiment

---

## Step 6: Add Reviewers

**Note:** You may skip this step if you have previously created user accounts.

1. Navigate to the **Users** screen
2. Click **Add User** in the upper left corner

![alt text](/docs/screen-shots/add-user.png)

3. Configure the user settings in the right panel:
   - **Name:** Enter the reviewer's full name
   - **Email:** Enter the reviewer's email address
   - **Roles:** Check `Reviewer`
   - **Expertise:** Select the option that best matches the reviewer's domain expertise
   - **Reviewer Type:** Select `Human Reviewer`
     - **Note:** If you have configured model endpoints, you can also create AI reviewers by selecting `Model Reviewer`
4. Click **Save** to add the user

---

## Step 7: Create an Assignment to Launch the Experiment

1. Navigate to the **Assignments** screen
2. Click **New Assignment** in the upper left corner
3. Configure the assignment settings in the right panel:
   - **Name:** `Open-ended Answers to Healthcare Questions`
   - **Description:** `Evaluate open-ended answers to healthcare questions`
   - **Experiment:** Select the experiment you created in Step 5
   - **Reviewers:** Select one or more users to assign the evaluation tasks
   - **Reviewer Instructions:** 
     - Click **Override** to create custom instructions for this assignment
     - Otherwise, the default instructions from the experiment will be used
   - **Randomization:** Enable **Enable Randomization** to shuffle the dataset elements before assigning them to reviewers
4. Click **Save** to create the assignment
5. Launch the assignment:

   - Click on the assignment you just created
   - Click the **Prepare** button in the upper toolbar
   - Wait until the **Processing** column status changes to `Prepared`
     - **Note:** This may take several minutes as the system generates model outputs using the configured endpoint
   - Click **Run** to assign the experiment to the specified reviewers
   - The **Status** column will change to `In Progress`

![alt text](/docs/screen-shots/launch-assignment.png)

---

## Step 8: Complete Evaluations as a Reviewer

1. Log in to HAIME using the credentials for the assigned reviewer account
2. Navigate to the **Arena** screen
3. You will see the assigned evaluation tasks listed

---![alt text](/docs/screen-shots/arena.png)

4. Click on an assignment to begin the evaluation process

---![alt text](/docs/screen-shots/arena-tutorial.png)

5. Review each model output and provide ratings according to the evaluation criteria
6. Submit your evaluations when complete

## Summary

You have successfully:
- Imported a healthcare Q&A dataset
- Configured an AI model endpoint
- Created a clinical task for evaluation
- Designed an experiment with custom evaluation metrics
- Set up reviewer accounts
- Created and launched an assignment
- Completed evaluations as a reviewer

For additional support or advanced features, please refer to the HAIME documentation or contact your system administrator.






    
