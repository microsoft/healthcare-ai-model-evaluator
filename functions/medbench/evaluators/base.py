from abc import ABC, abstractmethod
from typing import Any

import attrs

from medbench.models import (
    ModelRun,
    Runner,
    SystemPromptModel,
)


@attrs.define(kw_only=True)
class EvaluatorRunner(ABC):
    predictions_model_run: ModelRun
    evaluator: SystemPromptModel
    evaluator_runner: Runner = None

    @abstractmethod
    async def evaluate(self) -> Any:
        """Evaluate the predictions using the evaluator model."""
        raise NotImplementedError()
