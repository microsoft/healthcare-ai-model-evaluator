"""
Base TBFact implementation for factual consistency evaluation.

This module provides a standalone implementation of the TBFact evaluator
that can be used independently of MedBench's runner system. It extracts
facts from texts and evaluates their factual consistency.
"""
import os
import json
import logging
import re
from typing import Any, Dict, List, Optional

from .llm import LLMClient


class TBFactEvaluator:
    """
    Evaluates factual consistency between a generated text and a reference text.

    TBFact implements the following steps:
    1. Extracts facts from both the generated text and reference text
    2. Evaluates entailment of facts in both directions
    3. Calculates precision, recall, and F1 score

    TBFact does a bi-directional evaluation:
    - pred-to-gold: Evaluates if generated facts are supported by the reference (precision)
    - gold-to-pred: Evaluates if reference facts are captured in the generated text (recall)
    """

    FACT_EXTRACTION_PROMPT_TEMPLATE = """
Organize the following free-text passage into a list of facts. This list will be
later be used to evaluate the accuracy of each fact in the text.

- Each fact should be a single sentence. 
- Each fact should be categorized into one of the following categories:
    {fact_list}
- Include all factual statements from the text. Do not omit any facts.
- Maintain the order of the facts as they appear in the text.
- Facts that are repeated in the text should be included only once in the list of facts.

Here is the input text:

{input_text}

Format your response as a JSON array of objects, where each object has two properties:
- "fact": the text of the fact
- "category": the category of the fact (must be one of {fact_list})

Example:
[
  {{"fact": "Patient is a 65-year-old male", "category": "demographics"}},
  {{"fact": "Patient was diagnosed with stage II lung cancer", "category": "diagnosis"}}
]
"""

    ENTAILMENT_EVALUATION_PROMPT_TEMPLATE = """
Given the following list of facts, evaluate the entailment status. Assign one of the following labels to each fact:
- Yes: The fact is entailed by the reference text.
- No: The fact is not entailed by the reference text.
- Partial: The fact is partially entailed by the reference text.

In addition, for each fact, if the entailment status is 'No' or 'Partial', assign an error type. The error type should be one of the following:
- Missing: The fact is missing from the reference text.
- Incorrect: The fact is incorrect in the reference text.
- Ambiguous: The fact is ambiguous in the reference text.
- Other: The fact is not entailed by the reference text, but does not fall into any of the above categories.

Here is the list of facts:

{facts}

And here is the reference text:

{reference_text}

Format your response as a JSON array of objects, where each object has the following properties:
- "fact_idx": the index of the fact (as an integer)
- "entailment": "Yes", "No", or "Partial"
- "error_type": only for "No" or "Partial" entailment, one of "Missing", "Incorrect", "Ambiguous", "Other"

Example:
[
  {{"fact_idx": 0, "entailment": "Yes"}},
  {{"fact_idx": 1, "entailment": "No", "error_type": "Incorrect"}}
]
"""

    def __init__(
        self,
        llm_client: LLMClient,
        fact_categories: List[str] = None,
        fact_extraction_prompt_template: str = None,
        entailment_evaluation_prompt_template: str = None,
        reference_facts_cache: Dict[str, List[Dict[str, str]]] = None,
    ):
        """
        Initialize the TBFact evaluator.

        Args:
            llm_client: An LLM client that implements the LLMClient protocol
            fact_categories: List of fact categories to extract
            fact_extraction_prompt_template: Custom template for fact extraction prompt
            entailment_evaluation_prompt_template: Custom template for entailment evaluation prompt
            reference_facts_cache: Optional pre-extracted reference facts
        """
        self.llm_client = llm_client
        self.fact_categories = fact_categories or [
            "demographics",
            "diagnosis",
            "treatment",
            "symptom",
            "biomarker",
            "other",
        ]
        self.fact_list = ", ".join(f'"{fact}"' for fact in self.fact_categories)

        self.fact_extraction_prompt_template = (
            fact_extraction_prompt_template or self.FACT_EXTRACTION_PROMPT_TEMPLATE
        )
        self.entailment_evaluation_prompt_template = (
            entailment_evaluation_prompt_template
            or self.ENTAILMENT_EVALUATION_PROMPT_TEMPLATE
        )

        self.reference_facts_cache = reference_facts_cache or {}

    async def evaluate(
        self,
        generated_text: str,
        reference_text: str,
        reference_id: Optional[str] = None,
    ) -> Dict[str, Any]:
        """
        Evaluate factual consistency between generated and reference texts.

        Args:
            generated_text: The generated text to evaluate
            reference_text: The reference text to compare against
            reference_id: Optional identifier for caching reference facts

        Returns:
            Dictionary with evaluation results including metrics
        """
        # Extract facts from reference only once and cache them if provided ID
        reference_facts = None
        if reference_id and reference_id in self.reference_facts_cache:
            reference_facts = self.reference_facts_cache[reference_id]

        if reference_facts is None:
            reference_facts = await self._extract_facts(reference_text)
            if reference_id:
                self.reference_facts_cache[reference_id] = reference_facts
                logging.info(
                    f"Extracted {len(reference_facts)} facts from reference for id {reference_id}"
                )

        # Extract facts from generated text
        generated_facts = await self._extract_facts(generated_text)

        if not generated_facts or not reference_facts:
            logging.warning("Failed to extract facts from one or both texts")
            return {
                "score": 0.0,
                "explanation": "Failed to extract facts from one or both texts",
                "details": {
                    "error": "Fact extraction failed",
                    "generated_facts": generated_facts,
                    "reference_facts": reference_facts,
                },
            }

        # Evaluate entailment in both directions
        pred_to_gold_results = await self._evaluate_facts(
            generated_facts, reference_text
        )
        gold_to_pred_results = await self._evaluate_facts(
            reference_facts, generated_text
        )

        # Calculate metrics
        metrics = self._calculate_metrics(pred_to_gold_results, gold_to_pred_results)

        # Format individual fact evaluations for details
        pred_facts_eval = [
            {
                "fact": fact["fact"],
                "category": fact["category"],
                "entailment": result["entailment"],
                "error_type": result.get("error_type"),
                "direction": "pred_to_gold",
            }
            for fact, result in zip(generated_facts, pred_to_gold_results)
        ]

        gold_facts_eval = [
            {
                "fact": fact["fact"],
                "category": fact["category"],
                "entailment": result["entailment"],
                "error_type": result.get("error_type"),
                "direction": "gold_to_pred",
            }
            for fact, result in zip(reference_facts, gold_to_pred_results)
        ]

        # Calculate per-category metrics
        category_metrics = self._calculate_category_metrics(
            pred_facts_eval + gold_facts_eval
        )

        return {
            "score": metrics["f1"],  # Use F1 as the main score
            "explanation": f"Precision: {metrics['precision']:.3f}, Recall: {metrics['recall']:.3f}, F1: {metrics['f1']:.3f}",
            "details": {
                "metrics": metrics,
                "category_metrics": category_metrics,
                "fact_evaluations": pred_facts_eval + gold_facts_eval,
                "generated_facts": generated_facts,
                "reference_facts": reference_facts,
            },
        }

    def get_fact_extraction_prompt(self, input_text: str) -> str:
        """
        Create a fact extraction prompt for a given input text.

        Args:
            input_text: The text to extract facts from

        Returns:
            A formatted prompt for fact extraction
        """
        return self.fact_extraction_prompt_template.format(
            fact_list=self.fact_list, input_text=input_text
        )

    def get_entailment_evaluation_prompt(self, facts: str, reference_text: str) -> str:
        """
        Create an entailment evaluation prompt for facts against a reference text.

        Args:
            facts: Formatted facts to evaluate
            reference_text: Reference text to evaluate against

        Returns:
            A formatted prompt for entailment evaluation
        """
        return self.entailment_evaluation_prompt_template.format(
            facts=facts, reference_text=reference_text
        )

    async def _extract_facts(self, text: str) -> List[Dict[str, str]]:
        """
        Extract and categorize facts from text.

        Args:
            text: Text to extract facts from

        Returns:
            List of dictionaries with fact text and category
        """
        fact_extraction_prompt = self.get_fact_extraction_prompt(text)

        try:
            system_message = "You are a medical fact extraction assistant. Extract facts from medical text as JSON."
            completion = await self.llm_client.generate(
                system_message=system_message, user_message=fact_extraction_prompt
            )
            content = completion["content"]

            # Look for JSON pattern in the response
            json_match = re.search(r"\[\s*\{.*\}\s*\]", content, re.DOTALL)
            if json_match:
                json_str = json_match.group(0)
                facts = json.loads(json_str)
                return facts
            else:
                logging.error("No JSON found in fact extraction response")
                return []
        except Exception as e:
            logging.error(f"Error extracting facts: {e}")
            return []

    async def _evaluate_facts(
        self, facts: List[Dict[str, str]], reference_text: str
    ) -> List[Dict[str, str]]:
        """
        Evaluate entailment of facts against reference text.

        Args:
            facts: List of facts to evaluate
            reference_text: Reference text to check entailment against

        Returns:
            List of dictionaries with entailment judgments
        """
        if not facts:
            return []

        facts_formatted = "\n".join(
            [f"{i}: {fact['category']}: {fact['fact']}" for i, fact in enumerate(facts)]
        )

        entailment_prompt = self.get_entailment_evaluation_prompt(
            facts_formatted, reference_text
        )

        try:
            system_message = "You are a medical entailment evaluation assistant."
            completion = await self.llm_client.generate(
                system_message=system_message, user_message=entailment_prompt
            )
            content = completion["content"]

            # Look for JSON pattern in the response
            json_match = re.search(r"\[\s*\{.*\}\s*\]", content, re.DOTALL)
            if json_match:
                json_str = json_match.group(0)
                results = json.loads(json_str)
                return results
            else:
                logging.error("No JSON found in entailment evaluation response")
                return []
        except Exception as e:
            logging.error(f"Error evaluating entailment: {e}")
            return []

    def _calculate_metrics(
        self,
        pred_to_gold_results: List[Dict[str, str]],
        gold_to_pred_results: List[Dict[str, str]],
    ) -> Dict[str, float]:
        """
        Calculate precision, recall, and F1 from entailment results.

        Args:
            pred_to_gold_results: Results of evaluating predicted facts against gold
            gold_to_pred_results: Results of evaluating gold facts against predicted

        Returns:
            Dictionary with precision, recall, and F1 scores
        """
        # Convert entailment values to numerical scores
        entailment_values = {"Yes": 1.0, "Partial": 0.5, "No": 0.0}

        # Calculate precision (how many predicted facts are in gold)
        precision_values = [
            entailment_values.get(r.get("entailment", "No"), 0.0)
            for r in pred_to_gold_results
        ]
        precision = (
            sum(precision_values) / len(precision_values) if precision_values else 0.0
        )

        # Calculate recall (how many gold facts are in predicted)
        recall_values = [
            entailment_values.get(r.get("entailment", "No"), 0.0)
            for r in gold_to_pred_results
        ]
        recall = sum(recall_values) / len(recall_values) if recall_values else 0.0

        # Calculate F1
        f1 = (
            2 * (precision * recall) / (precision + recall)
            if (precision + recall) > 0
            else 0.0
        )

        return {
            "precision": precision,
            "recall": recall,
            "f1": f1,
            "precision_support": len(precision_values),
            "recall_support": len(recall_values),
        }

    def _calculate_category_metrics(
        self, fact_evaluations: List[Dict[str, Any]]
    ) -> Dict[str, Dict[str, float]]:
        """
        Calculate metrics broken down by fact category.

        Args:
            fact_evaluations: List of all fact evaluations

        Returns:
            Dictionary mapping categories to their metrics
        """
        # Group facts by category
        category_metrics = {}

        for category in self.fact_categories:
            # Filter facts by category
            category_facts = [f for f in fact_evaluations if f["category"] == category]
            if not category_facts:
                continue

            # Convert entailment values to numerical scores
            entailment_values = {"Yes": 1.0, "Partial": 0.5, "No": 0.0}

            # Split by direction
            pred_to_gold = [
                f for f in category_facts if f["direction"] == "pred_to_gold"
            ]
            gold_to_pred = [
                f for f in category_facts if f["direction"] == "gold_to_pred"
            ]

            # Calculate precision
            precision_values = [
                entailment_values.get(f["entailment"], 0.0) for f in pred_to_gold
            ]
            precision = (
                sum(precision_values) / len(precision_values)
                if precision_values
                else 0.0
            )

            # Calculate recall
            recall_values = [
                entailment_values.get(f["entailment"], 0.0) for f in gold_to_pred
            ]
            recall = sum(recall_values) / len(recall_values) if recall_values else 0.0

            # Calculate F1
            f1 = (
                2 * (precision * recall) / (precision + recall)
                if (precision + recall) > 0
                else 0.0
            )

            category_metrics[category] = {
                "precision": precision,
                "recall": recall,
                "f1": f1,
                "precision_support": len(precision_values),
                "recall_support": len(recall_values),
            }

        return category_metrics

    def load_reference_facts(self, filepath: str) -> bool:
        """
        Load reference facts from a JSON file.

        Args:
            filepath: Path to the JSON file containing reference facts

        Returns:
            True if loading was successful, False otherwise
        """
        if not os.path.exists(filepath):
            logging.error(f"Reference facts file not found: {filepath}. Skipping load.")
            return False

        try:
            with open(filepath, "r") as f:
                reference_facts = json.load(f)

            self.reference_facts_cache.update(reference_facts)
            logging.info(
                f"Successfully loaded reference facts for {len(reference_facts)} references"
            )
            return True
        except Exception as e:
            logging.error(f"Error loading reference facts: {e}")
            return False

    def save_reference_facts(self, filepath: str) -> bool:
        """
        Save cached reference facts to a JSON file.

        Args:
            filepath: Path where to save the reference facts JSON file

        Returns:
            True if saving was successful, False otherwise
        """
        try:
            with open(filepath, "w") as f:
                json.dump(self.reference_facts_cache, f, indent=2)

            logging.info(
                f"Successfully saved reference facts for {len(self.reference_facts_cache)} references"
            )
            return True
        except Exception as e:
            logging.error(f"Error saving reference facts: {e}")
            return False
