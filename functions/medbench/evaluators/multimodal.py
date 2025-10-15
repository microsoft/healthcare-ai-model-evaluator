"""Base Multimodal Evaluators."""

import logging
from typing import List

import attrs

from medbench.datasets import (
    Data,
    Dataset,
    EMediaObjectType,
    Instance,
    MediaObject,
)
from medbench.models import (
    Model,
    ModelOutput,
    ModelRegistry,
    ModelRun,
    Runner,
    SystemPromptModel,
)

from .base import EvaluatorRunner


@attrs.define(kw_only=True)
class MultimodalEvaluatorRunner(EvaluatorRunner):
    """Evaluator runner for multimodal evaluation.

    This evaluator implements a generic evaluation approach, where the
    evaluator model directly rates the output of the multimodal system.

    Attributes:
        predictions_model_run (ModelRun): ModelRun with the multimodal system predictions.
        evaluator (SystemPromptModel): Evaluator model.
        evaluator_runner (Runner): Evaluator runner.
        dataset_input_header (str): Header used to indicate the start of the dataset input.
        system_output_header (str): Header used to indicate the start of the system output.
        system_prompt (str): Evaluation system prompt.
            When modifying the system prompt, you may use the following placeholders:
            - {base_eval_prompt}: Main evaluation instructions and guidance.
            - {dataset_name}: Dataset name.
            - {dataset_description}: Dataset description.
            - {task_specific_eval}: Task-specific instructions and guidelines.
            - {output_specs_prompt}: Output guidelines.
        base_eval_prompt (str): Base evaluation prompt.
            This is injected into the system prompt if the corresponding placeholder is present.
        output_specs_prompt (str): Output guidelines prompt.
            This is injected into the system prompt if the corresponding placeholder is present.
        task_specific_eval_prompt (str): Task-specific evaluation prompt.
            This is injected into the system prompt if the corresponding placeholder is present.
    """

    predictions_model_run: ModelRun
    evaluator: SystemPromptModel
    evaluator_runner: Runner = None

    dataset_input_header: str = "DATASET INPUT:\n\n"
    system_output_header: str = "\n\nAI SYSTEM OUTPUT:\n\n"

    system_prompt: str = """\
{base_eval_prompt}

This time you will evaluate AI systems responses on the {dataset_name} dataset. \
Below is dataset description:
{dataset_description}

{task_specific_eval}

{output_specs_prompt}
"""

    base_eval_prompt: str = """\
You are an AI assistant with deep expertise in the medical domain. \
You're tasked with evaluating other AI systems in specific medical related tasks.

Independently of the task specifics, for a fair evaluation you shall:
- Evaluate the correctness of facts present in the answer;
- Evaluate relevancy of the given information to the task;
- Ensure the response covers all necessary aspects of the medical query or context;
- Evaluate whether the response includes hallucinated information, that the model could not have inferred from the context;

Ultimately, you should put yourself in the shoes of a medical professional and evaluate the response as if it was given by a human expert. \
You shall judge whether the response, if given in a real case scenario, would be helpful to you (a medical professional) to conduct your work \
in the best way possible.\
"""

    output_specs_prompt: str = """\
Following the evaluation guidelines, you must score each response with a whole number between 1 and 5, where:
- 1: The response is completely incorrect. Unusable by medical professionals.
- 2: The response is mostly incorrect, and cannot be used by medical professionals without major revisions.
- 3: The response is partially correct, and can point medical professionals in the right direction, but requires significant revisions.
- 4: The response is mostly correct, and can be used by medical professionals with minor revisions.
- 5: The response is complete and correct. Medical professionals can trust it as if it was given by a human expert.

Before scoring the response, think step by step about the evaluation guidelines and the task requirements. \
Revisit all evaluation criteria and be direct yet specific about the reasons for your score.

At the very end, you must present only the final score. Refrain from making any other remarks at this point. \
All in all your response shall match the following template:
```
Explanation:
All explanation and analysis of evaluation criteria must come here.

Score: Final score between 1 and 5.
```\

Your output is programmatically processed, so make sure to follow the template exactly.\
"""

    task_specific_eval_prompt: str = ""

    def __attrs_post_init__(self):
        if not self.predictions_model_run.results:
            raise ValueError(
                "ModelRun has no results. Please generate results before evaluating."
            )

        logging.info("Overriding evaluator system prompt.")
        self.evaluator = self.evaluator.evolve(
            system_prompt=self.system_prompt.format(
                base_eval_prompt=self.base_eval_prompt,
                dataset_name=self.predictions_model_run.dataset.name,
                dataset_description=self.predictions_model_run.dataset.description,
                task_specific_eval=self.task_specific_eval_prompt,
                output_specs_prompt=self.output_specs_prompt,
            )
        )

        if self.evaluator_runner is None:
            logging.info("Initializing evaluator runner.")
            evaluator_id = ModelRegistry.get_registered_name(type(self.evaluator))
            EvaluatorRunner = ModelRegistry.get_runner(evaluator_id)
            self.evaluator_runner = EvaluatorRunner(is_eval=True)

    async def evaluate(self) -> None:
        self.evaluator_runner.setup(
            self._prepare_evaluation_model_run(
                self.predictions_model_run, self.evaluator
            )
        )
        self.evaluator_runner.run()

    def _prepare_evaluation_model_run(
        self, model_run: ModelRun, model: Model
    ) -> ModelRun:
        evaluation_model_run = ModelRun(
            id=model_run.id,
            model=model,
            dataset=attrs.evolve(
                model_run.dataset,
                instances=self._prepare_evaluation_instances(
                    model_run.dataset.instances,
                    model_run.results,
                ),
            ),
        )

        return evaluation_model_run

    def _prepare_evaluation_instances(
        self,
        instances: List[Instance],
        results: List[ModelOutput],
    ) -> List[Instance]:
        eval_instances: List[Instance] = []
        for instance, result in zip(instances, results):
            if result.error is not None:
                # Only evaluate successful completions
                logging.debug(
                    f"Skipping instance {instance.id} due to inference error."
                )
                continue

            eval_instances.append(self._prepare_evaluation_instance(instance, result))

        logging.info(f"Prepared {len(eval_instances)} evaluation instances.")
        return eval_instances

    def _prepare_evaluation_instance(
        self, instance: Instance, result: ModelOutput
    ) -> Instance:
        images_content = []
        for media in instance.input.content:
            if media.type == EMediaObjectType.IMAGE:
                images_content.append(media)

        eval_input: Data = Data(
            content=[
                MediaObject(
                    type=EMediaObjectType.TEXT,
                    data=(
                        self.dataset_input_header
                        + instance.input.get_text()
                        + self.system_output_header
                        + result.completions.get_text()
                    ),
                )
            ]
            + images_content
        )

        return attrs.evolve(instance, input=eval_input)


@attrs.define(kw_only=True)
class ABEvaluatorRunner(MultimodalEvaluatorRunner):
    """Evaluator runner for A/B testing comparison evaluation."""

    predictions_model_run: ModelRun
    """ModelRun with the predictions of Model A."""
    predictions_model_run_b: ModelRun
    """ModelRun with the predictions of Model B."""

    def __attrs_post_init__(self):
        # Validate both model runs have results
        model_a_results = self.predictions_model_run.results
        model_b_results = self.predictions_model_run_b.results
        
        if not model_a_results or not model_b_results:
            raise ValueError("Both model runs must have results for A/B comparison.")
        
        if len(model_a_results) != len(model_b_results):
            raise ValueError("Model A and Model B must have the same number of results for comparison.")
        
        super().__attrs_post_init__()

    def _prepare_evaluation_model_run(
        self, model_run: ModelRun, model: Model
    ) -> ModelRun:
        """Prepare the evaluation model run for A/B testing."""
        # Combine results from both models
        model_a_results = self.predictions_model_run.results
        model_b_results = self.predictions_model_run_b.results
        
        # Create comparison instances
        comparison_instances = []
        for i, (result_a, result_b) in enumerate(zip(model_a_results, model_b_results)):
            # Skip if either result has an error
            if result_a.error is not None or result_b.error is not None:
                logging.debug(f"Skipping comparison {i} due to inference error.")
                continue
                
            comparison_input = f"""\
You have two model evaluation results below:

Model A Result: {result_a.completions.get_text()}

Model B Result: {result_b.completions.get_text()}
"""
            
            comparison_instances.append(
                Instance(
                    id=f"comparison_{i}",
                    input=Data(
                        content=[
                            MediaObject(
                                type=EMediaObjectType.TEXT, data=comparison_input
                            )
                        ]
                    ),
                    references=[],
                    split="test",
                )
            )
        
        # Create a comparison model run with the comparison data
        comparison_dataset = Dataset(
            name="ab_comparison",
            description="A/B comparison evaluation",
            instances=comparison_instances,
        )
        
        evaluation_model_run = ModelRun(
            id=model_run.id,
            model=model,
            dataset=comparison_dataset,
        )

        return evaluation_model_run