"""
LLM clients used in evaluators.
"""

import attrs
from typing import Any, Dict, Optional, Protocol, runtime_checkable
from medbench.models import (
    ModelOutput,
    ModelRegistry,
    ModelRun,
    Runner,
    SystemPromptModel,
)
from medbench.datasets import (
    CORRECT_TAG,
    Data,
    Dataset,
    EMediaObjectType,
    Instance,
    MediaObject,
    Reference,
)
import logging
@runtime_checkable
class LLMClient(Protocol):
    """Protocol defining the interface for LLM clients used with evaluators."""

    async def generate(self, system_message: str, user_message: str) -> Dict[str, Any]:
        """
        Generate a completion from the LLM.

        Args:
            system_message: The system message for the completion
            user_message: The user message for the completion

        Returns:
            Dictionary containing at least a "content" key with the completion text
        """
        ...

@attrs.define
class MedBenchLLMClientAdapter:
    """
    Adapter class that wraps a MedBench model to provide an
    implementation of the LLMClient protocol for the TBFactEvaluator.
    """

    model: SystemPromptModel
    runner: Runner

    async def generate(self, system_message: str, user_message: str) -> Dict[str, Any]:
        """
        Generate a completion using the MedBench model.

        Args:
            system_message: The system message for the completion
            user_message: The user message for the completion

        Returns:
            Dictionary containing the completion content
        """
        temp_model = self.model.evolve(system_prompt=system_message)

        # Create a temporary dataset with just this instance
        instance = Instance(
            id="temp",
            input=Data.from_text(data=user_message),
            references=[],
            split="temp"
        )

        # Create a model run
        model_run = ModelRun(
            id="temp",
            model=temp_model,
            dataset=Dataset(
                name="temp",
                description="Temporary dataset for TBFact evaluation",
                instances=[instance],
            ),
        )

        # Run inference
        self.runner.setup(model_run)
        self.runner.run()

        # Get the result
        result = model_run.results[0]
        if result.error:
            raise ValueError(f"Model inference failed: {result.error}")

        return {"content": result.completions.get_text()}
