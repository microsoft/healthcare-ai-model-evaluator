[Healthcare AI Model Evaluator - Data Model](https://microsoft.sharepoint.com/:u:/t/BabelBenchforHealth/EQfzRlizy0dKjnkQoTqpQicBKydadPyvjHADy8O6mTTQmA?e=KJw9K0)

  (from loop page: [Healthcare AI Model Evaluator Conceptual Model](https://microsoft.sharepoint.com/:fl:/s/63f2e58a-766f-4ca2-9b14-3c5f03640cb4/EXk8wlEd39tCskNZ7Zim5D8BP90Qz4AnaGPTYYSIqPITvQ?e=aLlwAT&nav=cz0lMkZzaXRlcyUyRjYzZjJlNThhLTc2NmYtNGNhMi05YjE0LTNjNWYwMzY0MGNiNCZkPWIlMjFnckRCd1UyVTJrZUk5dEZIdTBfTnlCNlY0eWFaeGs1TWxYQjB5dHBxT0lTNmRWUnYtbVVfU29vRGo0enZkTTZaJmY9MDFHSU4yMlMzWkhUQkZDSE83M05CTEVRMlo1V01LTlpCNyZjPSUyRiZhPUxvb3BBcHAmcD0lNDBmbHVpZHglMkZsb29wLXBhZ2UtY29udGFpbmVyJng9JTdCJTIydyUyMiUzQSUyMlQwUlRVSHh0YVdOeWIzTnZablF1YzJoaGNtVndiMmx1ZEM1amIyMThZaUZuY2tSQ2QxVXlWVEpyWlVrNWRFWklkVEJmVG5sQ05sWTBlV0ZhZUdzMVRXeFlRakI1ZEhCeFQwbFRObVJXVW5ZdGJWVmZVMjl2UkdvMGVuWmtUVFphZkRBeFIwbE9NakpUV1VKRVNFeEtUelZOUlZvMVFsbEpXRTlhUjFsWFJrRXpURWclM0QlMjIlMkMlMjJpJTIyJTNBJTIyNmY5NzQxNTAtNTczNS00Y2ZkLTk4MjgtZmEwMDU3OGRlNmQxJTIyJTdE))

![==image_0==.png](/docs/.attachments/==image_0==-888fe102-2fba-4968-a212-2da88f04713a.png) 

  

### Data Types

Built-in, supported data types used in AI models as input and/or output.

  

Initial set to be supported:

*   Standard Text
*   Json data
*   Coordinates
*   Bounding box
*   Image (pixel)
*   Embeddings
  

### Prompt - Input configuration

Text to be used as input to trigger the output from the AI model.

Prompt text can be a template and include parameters to be filled in a staging step.

  
*   Prompt text
*   Transformation recipe (optional)
*   Parameters (optional)

### Model Type

The type of AI model as defined by the input data types it expects and output data types it delivers.

In principle, any data type to any other data type output (or combinations thereof) should be supported. However we will start with the following initial set:

*   Text to Text
*   Text to Image
*   Image to Text
*   Image to (Text + Bounding box)
*   (Image + Text ) to Text
*   Text to Image
*   Text to Embeddings
*   Image to Embeddings
*   ...
*   Tags
  

### Dataset

The full collection of data elements required to run an experiment.

Input data and output data must conform to data types.

Ground truth or reference data may be included, but is not necessarily required for Arena type of experiments.

*   Origin/Source
*   Description
*   Supported AI models (Model Type)
*   Input data (Data Type)
*   Ouput data (Data Type)
*   Prompt (optional) and parameters
*   Ground truth / Reference data (optional, Data Type)
*   Tags
  
  
  
  
  

### Clinical Tasks

This is the fundamental unit for evaluating a clinical use-case.

It is a complete configuration consisting of the required dataset(s) and prompts. It is then included in a test scenario to be executed.

*   Description
*   Datasets
*   Prompt (can re-use dataset provided prompt as needed)
*   Tags

### Evaluation Metrics

Prebuilt set of metrics to measure outputs against

*   Text based metrics
*   Image based metrics
*   Accuracy metrics (closed ended datasets)
*   Safety metrics
*   Bias metrics
*   ....
  

### Test Scenario

Test scenario combines a clinical task with necessary components to create a full evaluation.

It consists of:

*   Description
*   Clinical Task to be evaluated
*   AI Models to generate output to be evaluated
*   Evaluation metric(s) to generate evaluation criteria
  

### Expert Reviewer

Definition and identification of expert evaluators, who will be performing the evaluation tasks

  
*   Name
*   Credentials
*   Expert level
*   Contact Info / e-mail
*   Affiliation
*   Clinical Tasks to which evaluator can be assigned

### AI Model

The basic definition of a AI model which can consume the inputs and generate the outputs as defined.

*   Origin
*   Description
*   Config Parameters
*   Model Type
*   Tags
*   ...

### Experiment Type

The basic experiment workflow to be followed when running. It can be one of the following

*   Simple evaluation (one output, one criterion, pre-defined feedback)
*   Simple validation (one output, one criterion, open ended feedback)
*   Arena (select one output between two, one criterion)
*   Full validation (one output, multiple criterion, open ended feedback)
*   ...

### Experiment Design

The full configuration and all necessary elements to run an experiment with human feedback. It combines:

*   Test scenario
*   Expert Reviewer(s)
*   Experiment type