import logging
from abc import abstractmethod
from typing import Generator, Self

import attrs

from medbench.datasets import Data
from medbench.models import ModelRun


def get_evaluation_pairs(
    model_run: ModelRun,
) -> Generator[tuple[list[Data], Data], None, None]:
    """Get evaluation pairs from a model run."""
    for input, result in zip(model_run.dataset.instances, model_run.results):
        yield input.get_ground_truths(), result.completions


def aggregate_metrics(metrics: list[dict[str, float]]) -> dict[str, float]:
    """Aggregate metrics for a model run."""
    metric_values = {}
    for instance_level_metrics in metrics:
        for metric, value in instance_level_metrics.items():
            if metric not in metric_values:
                metric_values[metric] = []
            metric_values[metric].append(value)

    aggregated_metrics = {
        metric: sum(values) / len(values) for metric, values in metric_values.items()
    }

    return aggregated_metrics


@attrs.define
class Metric:
    @classmethod
    @abstractmethod
    def setup() -> Self:
        raise NotImplementedError

    def calculate(self, ground_truths: list[Data], prediction: Data) -> float | str:
        """Calculate the metric for a given ground truth and prediction."""
        try:
            return self._calculate(ground_truths, prediction)
        except Exception as e:
            logging.error(
                f"Error calculating metric {self.__class__.__name__}: {e}"
            )
            logging.info(f"Ground truths: {ground_truths}, Prediction: {prediction}")
            raise e
            return str(e)

    @abstractmethod
    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        raise NotImplementedError
