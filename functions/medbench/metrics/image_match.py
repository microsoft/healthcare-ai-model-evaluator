from medbench.datasets import Data
from medbench.models import ModelRun

from .base import get_evaluation_pairs, aggregate_metrics


def calculate_image_metrics(model_run: ModelRun) -> list[dict[str, float]]:
    """Calculate image metrics for a model run."""
    instance_level_metrics = []
    for ground_truths, prediction in get_evaluation_pairs(model_run):
        instance_level_metrics.append(
            {
                "mean_pixel_accuracy": calculate_mean_pixel_accuracy(
                    ground_truths, prediction
                ),
                # "mean_iou": calculate_mean_iou(ground_truths, prediction),
                # "mean_dice": calculate_mean_dice(ground_truths, prediction),
            }
        )

    metrics = {
        "aggregated_metrics": aggregate_metrics(instance_level_metrics),
        "instance_level_metrics": instance_level_metrics,
    }

    return metrics


def calculate_mean_pixel_accuracy(ground_truths: list[Data], prediction: Data) -> float:
    """Calculate mean pixel accuracy for a model run."""
    # Placeholder implementation
    return 0.0
