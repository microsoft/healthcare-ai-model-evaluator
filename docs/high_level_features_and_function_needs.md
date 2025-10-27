Healthcare AI Model Evaluator collaboration framework
--------------------------------
(from the loop page): [Healthcare AI Model Evaluator High-level Feature Function Needs](https://microsoft.sharepoint.com/:fl:/s/63f2e58a-766f-4ca2-9b14-3c5f03640cb4/EXMidEMtfLlJiokxkHYO_sMB8LAXgObY2p5CMlZVw1ED0A?e=Xv6hka&nav=cz0lMkZzaXRlcyUyRjYzZjJlNThhLTc2NmYtNGNhMi05YjE0LTNjNWYwMzY0MGNiNCZkPWIlMjFnckRCd1UyVTJrZUk5dEZIdTBfTnlCNlY0eWFaeGs1TWxYQjB5dHBxT0lTNmRWUnYtbVVfU29vRGo0enZkTTZaJmY9MDFHSU4yMlMzVEVKMkVHTEw0WEZFWVZDSlJTQjNBNTdXRCZjPSUyRiZhPUxvb3BBcHAmcD0lNDBmbHVpZHglMkZsb29wLXBhZ2UtY29udGFpbmVyJng9JTdCJTIydyUyMiUzQSUyMlQwUlRVSHh0YVdOeWIzTnZablF1YzJoaGNtVndiMmx1ZEM1amIyMThZaUZuY2tSQ2QxVXlWVEpyWlVrNWRFWklkVEJmVG5sQ05sWTBlV0ZhZUdzMVRXeFlRakI1ZEhCeFQwbFRObVJXVW5ZdGJWVmZVMjl2UkdvMGVuWmtUVFphZkRBeFIwbE9NakpUV1VKRVNFeEtUelZOUlZvMVFsbEpXRTlhUjFsWFJrRXpURWclM0QlMjIlMkMlMjJpJTIyJTNBJTIyMDUyMzRmYmMtYTI1OC00ZmJlLThiNjMtNGM1MzliMjE3MDkyJTIyJTdE)

![Benchmarking collaboration framework: Diagram describing proposed responsibilities between Microsoft and Partners](/docs/.attachments/==image_0==-89a9a13a-8d63-4409-89c4-e8e0628724f6.png) 

Principles:

*   Microsoft jump starts HLS Benchmarking by providing all layers for oft cited, selected use-cases. This includes, prompts, data, model endpoints and reporting infrastructure, starting with standard, closed-ended (multiple-choice) medical datasets
*   Microsoft expands the breadth of use-cases and model end-points, and opens up the framework to selected partners (left side)
*   Microsoft enables internal and external partners/stakeholders to define their own datasets, use-cases and endpoints (right side).
  

![Benchmarking collaboration framework: Diagram showing benchmarking steps](/docs/.attachments/==image_1==-62bf1088-861e-4616-8c7f-1ad7611aaff7.png) 

  

Propose Roadmap Progression
---------------------------

![Proposed roadmap structured by benchmarking steps](/docs/.attachments/==image_2==-20628e53-f775-41a6-a556-e52151d3ce19.png) 

1st phase (October – December 2024)

*   Team formation and readiness
*   Expansion of supported publicly available, HLS-relevant datasets
*   Human Evaluation: Grading & Arena (text only)
*   Selected customer / stakeholder engagements to define / refine new use-cases
  

2nd phase (January – June 2024)

*   Expansion of Arena functionality into multimodal use-cases
*   Expansion into custom datasets (feed forward from Phase-1 Arena functionality)
*   Capabilities to add/manage Data and LLM-endpoints from sources with varying rights/restrictions
    *   Private/public visibility
    *   Ability to use data/endpoint from own (customer’s) subscription
    *   Ability to use data/endpoint from 3rd party sources

![Proposed roadmap with dates](/docs/.attachments/==image_3==-bfd3f7c6-84a3-4e23-9aa5-77a4ed3a1e01.png) 

Capability Build-up to implement the roadmap
--------------------------------------------

### Dataset expansion (phase 1):

*   Add following publicly available, healthcare-centric datasets for evalution into Healthcare AI Model Evaluator controlled storage

Open Ended (multiple choice) Datasets:

    *   MedQA, including USMLE
    *   Head-QA
    *   PubMedQA
    *   BioMistral
    *   MedMCQA
    *   MedBullets

  

### Evaluation Framework:

*   Build GUI/UX and backend infrastructure for arena functionality (See draft wireframes below)
    *   ability to evaluate text outputs (1 output at a time)
    *   ability to compare text outputs from 2 different sources
    *   ability to evaluate multi-modal outputs (1 output at a time)
    *   ability to compare multi-modal outputs from 2 different sources
  
*   Enable human evaluation for internal stakeholders (Microsoft credentials)
  

### Arena Workflows

Review / Evaluate Pre-processed results

Review / Evaluate results generated in real-time

  

Assign a human designated set of input/output pairs

to specific users

to randomly selected users

  

Assign a randomly selected set of input / output pairs

to specific users

to randomly selected users

  

End user can

*   skip individual cases
*   go back to skipped cases
*   flag a case for patient safety / PHI leakage / bias / ...
*   comment on individual sections or all of any given output
*   review their answers in a given batch / session
  
  

### Evaluation methods

Text:

*   algorithmic / deterministic
    *   surface matching: bleu, rouge, meteor, f1, ...
    *   semantic matching: bertScore, cosine similarity, alignscore and similar
*   model (LLM) as a judge
*   human as a judge
*   hybrid (human + model + algorithm)

Pixel:

*   Algorithmic / deterministic: Open
    *   mean Average Precision (mAP), Intersection over Union (IoU / aka Jaccard index), Mean Pixel Accuracy, Hausdorff distance, Rand index, pixel-wise classification error, SSIM
    *   VQA (text evaluation will be used)
*   [Multimodal LLM-as-a-judge](https://arxiv.org/pdf/2402.04788)
*   human as a judge
  
  
  

End-point management
--------------------

### Model end-point

*   (Built-in) HLS-specific model endpoints support / Healthcare AI Model Evaluator controlled area
*   (Built-in / Addon) Other MSFT supported model endpoints support (e.g. DAX)
*   (Add-on) Customer model endpoints support (e.g. M42)
*   (Add-on) On-prem / edge / small model support (e.g. fine-tuned phi3)
  

### Data storage

*   local storage
*   fabric support
*   3rd party blob storage (other cloud providers)
  
*   Hosting control WRT security & privacy
*   Nutrition label WRT HIPAA, PHI, data rights / restrictions
  

Evaluation Data-set attributes
------------------------------

*   Closed questions: True/False or multiple choice
*   Open-questions: answer (output) is free-form language, whose validity / correctness / safety must be evaluated
*   Gold-set: confidence level (auto-generated / human reviewed / human verified) ...
  
*   Open source / public data
*   Synthetic data (incl. seeded with de-id data)
*   licensed data (licensor to de-id data)
*   customer shared data (customer to de-id data)
*   private data (only visible to the customer/owner of data)
  

Modalities:
-----------

*   Text only: Text --> text
*   Multimodal
    *   Text --> image
    *   Text + image --> image
    *   Image --> image
    *   Image --> text
    *   image --> image + text
    *   text + image --> image + text
    *   +Multiple images
    *   +Video