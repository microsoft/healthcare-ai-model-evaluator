"""
MedBench TBFactEvaluatorRunner: integrates the standalone TBFactEvaluator and exposes all intermediate steps as ModelRuns.
"""

import asyncio
import logging
from typing import List, Optional, Generator, Tuple, Dict, Any

import attrs

from medbench.datasets import (
    CORRECT_TAG,
    Data,
    Dataset,
    EMediaObjectType,
    Instance,
    MediaObject,
    Reference,
)
from medbench.evaluators import EvaluatorRunner
from medbench.models import (
    ModelOutput,
    ModelRegistry,
    ModelRun,
    Runner,
    SystemPromptModel,
)

from .llm import MedBenchLLMClientAdapter
from .tbfact import TBFactEvaluator


@attrs.define(kw_only=True)
class TBFactEvaluatorRunner(EvaluatorRunner):
    """
    MedBench runner for TBFact factual consistency evaluation.

    This class is a wrapper around the TBFactEvaluator, that transforms the
    evaluation results into relevant ModelRuns instances:
    - Fact extraction (from both prediction and reference)
    - Entailment analysis (for each fact/reference pair, both directions)
    - Final scoring (with scores in metadata)

    All evaluation logic is delegated to the TBFactEvaluator class.
    These ModelRuns integrate with the MedBench environment.

    Attributes:
        predictions_model_run (ModelRun): ModelRun containing the predictions to evaluate.
            TBFact requires ground truth references to be present in the dataset, instances
            without references will be skipped.
        evaluator (SystemPromptModel): The model used for evaluation.
        evaluator_runner (Runner): The runner for the evaluator model.
            Defaults to None, and will be automatically created from the ModelRegistry.
        batch_size (int): Number of instances to process in a single batch.
            Defaults to 1.
            Used for parallelizing calls to TBFactEvaluator.`
        reference_facts_path (str): Path to the reference facts file.
            If provided, the TBFactEvaluator will load these facts for evaluation.
        fact_categories (List[str]): List of fact categories to evaluate.
            Passed to the TBFactEvaluator.
        fact_extraction_prompt_template (str): Prompt template for fact extraction.
            Passed to the TBFactEvaluator.
        entailment_evaluation_prompt_template (str): Prompt template for entailment evaluation.
            Passed to the TBFactEvaluator.
        tbfact (TBFactEvaluator): The TBFactEvaluator instance.
        tbfact_evaluation_model_run (ModelRun): ModelRun for TBFact evaluation.
        fact_extraction_model_run (ModelRun): ModelRun for fact extraction.
        entailment_model_run (ModelRun): ModelRun for entailment analysis.
    """

    predictions_model_run: ModelRun
    evaluator: SystemPromptModel
    evaluator_runner: Optional[Runner] = None

    batch_size: int = 5

    # TBFact-specific parameters
    reference_facts_path: Optional[str] = None
    fact_categories: Optional[List[str]] = None
    fact_extraction_prompt_template: Optional[str] = None
    entailment_evaluation_prompt_template: Optional[str] = None

    tbfact: TBFactEvaluator = attrs.field(init=False)
    tbfact_evaluation_model_run: ModelRun = attrs.field(init=False)
    fact_extraction_model_run: ModelRun = attrs.field(init=False)
    entailment_model_run: ModelRun = attrs.field(init=False)

    def __attrs_post_init__(self):
        if self.evaluator_runner is None:
            evaluator_id = ModelRegistry.get_registered_name(type(self.evaluator))
            EvaluatorModelRunner = ModelRegistry.get_runner(evaluator_id)
            self.evaluator_runner = EvaluatorModelRunner(is_eval=True)

        # Prepare LLM adapter and TBFactEvaluator
        self.tbfact = TBFactEvaluator(
            llm_client=MedBenchLLMClientAdapter(self.evaluator, self.evaluator_runner),
            fact_categories=self.fact_categories,
            fact_extraction_prompt_template=self.fact_extraction_prompt_template,
            entailment_evaluation_prompt_template=self.entailment_evaluation_prompt_template,
        )
        if self.reference_facts_path:
            self.tbfact.load_reference_facts(self.reference_facts_path)

        self.tbfact_evaluation_model_run = ModelRun(
            id=f"{self.predictions_model_run.id}-tbfact-evaluation",
            model=self.evaluator,
            dataset=Dataset(
                name=f"{self.predictions_model_run.dataset.name}-tbfact-evaluation",
                description="End to end TBFact evaluation dataset.",
                instances=[],
            ),
            results=[],
        )

        self.fact_extraction_model_run = ModelRun(
            id=f"{self.predictions_model_run.id}-tbfact-fact-extraction",
            model=self.evaluator,
            dataset=Dataset(
                name=f"{self.predictions_model_run.dataset.name}-tbfact-fact-extraction",
                description="TBFact Fact Extraction dataset.",
                instances=[],
            ),
            results=[],
        )

        self.entailment_model_run = ModelRun(
            id=f"{self.predictions_model_run.id}-tbfact-entailment",
            model=self.evaluator,
            dataset=Dataset(
                name=f"{self.predictions_model_run.dataset.name}-tbfact-entailment",
                description="TBFact Entailment Analysis dataset.",
                instances=[],
            ),
            results=[],
        )

    async def evaluate(self) -> List[Dict[str, Any]]:
        """
        Run TBFact evaluation workflow, creating ModelRuns for:
        - Fact extraction (from both prediction and reference)
        - Entailment analysis (all fact/text pairs, both directions)
        - Final scoring (with scores in metadata)
        """
        logging.info(f"Running TBFact evaluator workflow for {self.predictions_model_run.id}")

        # Process instances in batches
        for batch in self._create_batches(
            self.predictions_model_run.dataset.instances,
            self.predictions_model_run.results,
            self.batch_size
        ):
            # Create and run evaluation tasks for this batch
            batch_tasks = []
            batch_contexts = []
            tbfact_results: List[Dict[str, Any]] = []
            
            for instance, prediction in batch:
                # Extract texts
                prediction_text = prediction.completions.get_text()
                reference_text = "\n\n".join(
                    [
                        ref.output.get_text()
                        for ref in instance.references
                        if CORRECT_TAG in ref.tags
                    ]
                )
                
                # Create evaluation task
                task = self.tbfact.evaluate(
                    generated_text=prediction_text,
                    reference_text=reference_text,
                    reference_id=instance.id,
                )
                batch_tasks.append(task)
                batch_contexts.append({
                    'instance': instance,
                    'prediction': prediction,
                    'prediction_text': prediction_text,
                    'reference_text': reference_text
                })
            
            # Await batch results
            batch_results = await asyncio.gather(*batch_tasks)
            
            tbfact_results.extend(batch_results)
            logging.info(
                f"TBFact evaluation batch complete for {self.predictions_model_run.id}."
            )

            # Process results
            for eval_result, context in zip(batch_results, batch_contexts):
                self._add_main_evaluation_result(
                    context['instance'], 
                    context['prediction'],
                    context['reference_text'],
                    eval_result
                )
                
                # Only add detailed results if evaluation was successful
                # Check if this is an error result (missing expected structure)
                details = eval_result.get("details", {})
                if "error" in details:
                    logging.warning(f"Skipping detailed results for instance {context['instance'].id} due to evaluation error: {details.get('error')}")
                    continue
                
                self._add_fact_extraction_results(
                    context['instance'],
                    context['prediction_text'],
                    context['reference_text'],
                    eval_result
                )
                self._add_entailment_results(
                    context['instance'],
                    context['prediction_text'],
                    context['reference_text'],
                    eval_result
                )

        logging.info(
            f"TBFact evaluation complete for {self.predictions_model_run.id}."
        )
        return tbfact_results

    def _create_batches(
        self, 
        instances: List[Instance], 
        predictions: List[ModelOutput], 
        batch_size: int
    ) -> Generator[List[Tuple[Instance, ModelOutput]], None, None]:
        """
        Create batches of valid instance/prediction pairs for processing.
        
        Args:
            instances: List of dataset instances
            predictions: List of model predictions
            batch_size: Number of items per batch
            
        Yields:
            List of (instance, prediction) tuples for each batch
        """
        batch = []
        
        for instance, prediction in zip(instances, predictions):
            if prediction.error is not None:
                logging.warning(
                    f"Instance {instance.id} has no associated prediction: {prediction.error}"
                )
                continue

            if not instance.references:
                logging.warning(
                    f"Instance {instance.id} has no references: skipping TBFact evaluation."
                )
                continue
            
            batch.append((instance, prediction))
            
            # Yield when batch is full
            if len(batch) == batch_size:
                yield batch
                batch = []
        
        # Yield any remaining items
        if batch:
            yield batch

    def _add_main_evaluation_result(
        self, 
        instance: Instance, 
        prediction: ModelOutput,
        reference_text: str,
        eval_result: dict
    ) -> None:
        """Add main TBFact evaluation result to the model run."""
        self.tbfact_evaluation_model_run.dataset.instances.append(
            Instance(
                id=instance.id,
                input=prediction.completions,
                references=[Reference(output=Data.from_text(data=reference_text))],
                split=instance.split,
                sub_split=instance.sub_split,
            )
        )
        self.tbfact_evaluation_model_run.results.append(
            ModelOutput(
                input_id=instance.id,
                completions=f"Score: {eval_result['score']}\n\nExplanation: {eval_result['explanation']}",
                metadata=eval_result,
            )
        )

    def _add_fact_extraction_results(
        self,
        instance: Instance,
        prediction_text: str,
        reference_text: str,
        eval_result: dict
    ) -> None:
        """Add fact extraction results to the model run."""
        details = eval_result.get("details", {})
        generated_facts = details.get("generated_facts", [])
        reference_facts = details.get("reference_facts", [])
        
        if not generated_facts and not reference_facts:
            logging.warning(f"No facts found in eval_result for instance {instance.id}")
            return
            
        for kind, text, facts in [
            (
                "prediction",
                prediction_text,
                generated_facts,
            ),
            (
                "reference",
                reference_text,
                reference_facts,
            ),
        ]:
            # Instance for this input
            self.fact_extraction_model_run.dataset.instances.append(
                Instance(
                    id=f"{instance.id}-{kind}",
                    input=Data.from_text(data=text),
                    references=[],
                    split=instance.split,
                    sub_split=instance.sub_split,
                )
            )

            # Result: each claim as a MediaObject
            fact_media = [
                MediaObject.from_text(data=f["fact"])
                for f in facts
            ]
            self.fact_extraction_model_run.results.append(
                ModelOutput(
                    input_id=f"{instance.id}-{kind}",
                    completions=Data(content=fact_media),
                )
            )

    def _add_entailment_results(
        self,
        instance: Instance,
        prediction_text: str,
        reference_text: str,
        eval_result: dict
    ) -> None:
        """Add entailment analysis results to the model run."""
        # Check if fact_evaluations exists in the result
        details = eval_result.get("details", {})
        fact_evaluations = details.get("fact_evaluations", [])
        
        if not fact_evaluations:
            logging.warning(f"No fact_evaluations found in eval_result for instance {instance.id}")
            return
            
        direction_fact_counter = {}
        for fact_eval in fact_evaluations:
            direction = fact_eval["direction"]
            target_text = (
                prediction_text if direction == "gold_to_pred" else reference_text
            )

            if direction not in direction_fact_counter:
                direction_fact_counter[direction] = 0
            direction_fact_counter[direction] += 1

            # Instance input: extracted fact and target text as MediaObjects
            self.entailment_model_run.dataset.instances.append(
                Instance(
                    id=f"{instance.id}-{direction}-fact_{direction_fact_counter[direction]}",
                    input=Data(
                        content=[
                            MediaObject.from_text(data=fact_eval["fact"]),
                            MediaObject.from_text(data=target_text),
                        ]
                    ),
                    references=[],
                    split=instance.split,
                    sub_split=instance.sub_split,
                )
            )
            self.entailment_model_run.results.append(
                ModelOutput(
                    input_id=f"{instance.id}-{direction}-fact_{direction_fact_counter[direction]}",
                    completions=Data.from_text(data=fact_eval["entailment"]),
                    metadata=fact_eval,
                )
            )
