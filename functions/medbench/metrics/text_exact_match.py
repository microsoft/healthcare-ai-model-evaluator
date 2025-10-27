import attrs
import evaluate

from medbench.datasets import Data
from medbench.models import ModelRun

from .base import Metric, get_evaluation_pairs, aggregate_metrics


def calculate_exact_match_metrics(model_run: ModelRun) -> list[dict[str, float]]:
    """Calculate exact match metrics for a model run."""
    exact_match = ExactMatchMetric.setup()

    instance_level_metrics = []
    for ground_truths, prediction in get_evaluation_pairs(model_run):
        instance_level_metrics.append(
            {
                "exact_match": exact_match.calculate(ground_truths, prediction),
            }
        )

    metrics = {
        "aggregated_metrics": aggregate_metrics(instance_level_metrics),
        "instance_level_metrics": instance_level_metrics,
    }

    # TODO: F1 and Accuracy are aggregated metrics,
    # so they must be calculated for the entire model run.
    # "f1": calculate_f1(ground_truths, prediction),
    # "accuracy": calculate_accuracy(ground_truths, prediction),

    return metrics


@attrs.define
class ExactMatchMetric(Metric):
    metric: evaluate.EvaluationModule

    @classmethod
    def setup(cls) -> "ExactMatchMetric":
        metric = evaluate.load("exact_match")
        return cls(metric=metric)

    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        """Calculate exact match metric for a model run."""
        results = [
            self.metric.compute(
                predictions=[prediction.get_text()],
                references=[
                    gt.get_text(),
                ],
                ignore_whitespace=True,
                ignore_case=True,
                ignore_punctuation=True,
            )["exact_match"]
            for gt in ground_truths
        ]

        # Either 1 or 0
        return max(results)


# def calculate_f1(ground_truths: list[Data], prediction: Data) -> float:
#     """Calculate F1 metric for a model run."""
#     # Placeholder implementation
#     return 0.0


# def calculate_accuracy(ground_truths: list[Data], prediction: Data) -> float:
#     """Calculate accuracy metric for a model run."""
#     return 0.0