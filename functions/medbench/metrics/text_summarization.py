from abc import ABC

import attrs
import evaluate
from evaluate import EvaluationModule

from medbench.datasets import Data
from medbench.models import ModelRun

from .base import Metric, get_evaluation_pairs, aggregate_metrics


def calculate_summarization_metrics(model_run: ModelRun) -> list[dict[str, float]]:
    """Calculate summarization metrics for a model run."""
    rouge1 = Rouge1Metric.setup()
    rouge2 = Rouge2Metric.setup()
    rougeL = RougeLMetric.setup()
    bleu = BleuMetric.setup()
    meteor = MeteorMetric.setup()
    bert_score = BertScoreMetric.setup()

    instance_level_metrics = []
    for ground_truths, prediction in get_evaluation_pairs(model_run):
        instance_level_metrics.append(
            {
                "rouge1": rouge1.calculate(ground_truths, prediction),
                "rouge2": rouge2.calculate(ground_truths, prediction),
                "rougeL": rougeL.calculate(ground_truths, prediction),
                "bleu": bleu.calculate(ground_truths, prediction),
                "meteor": meteor.calculate(ground_truths, prediction),
                "bert_score": bert_score.calculate(ground_truths, prediction),
            }
        )

    metrics = {
        "aggregated_metrics": aggregate_metrics(instance_level_metrics),
        "instance_level_metrics": instance_level_metrics,
    }

    return metrics


@attrs.define
class RougeMetric(Metric, ABC):
    metric: EvaluationModule
    use_stemmer: bool = True

    @classmethod
    def setup(cls) -> "Rouge1Metric":
        rouge = evaluate.load("rouge")
        return cls(metric=rouge)


@attrs.define
class Rouge1Metric(RougeMetric):
    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        return self.metric.compute(
            predictions=[prediction.get_text()],
            references=[gt.get_text() for gt in ground_truths],
            rouge_types=["rouge1"],
            use_stemmer=self.use_stemmer,
        )["rouge1"]


@attrs.define
class Rouge2Metric(RougeMetric):
    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        return self.metric.compute(
            predictions=[prediction.get_text()],
            references=[gt.get_text() for gt in ground_truths],
            rouge_types=["rouge2"],
            use_stemmer=self.use_stemmer,
        )["rouge2"]


@attrs.define
class RougeLMetric(RougeMetric):
    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        return self.metric.compute(
            predictions=[prediction.get_text()],
            references=[gt.get_text() for gt in ground_truths],
            rouge_types=["rougeL"],
            use_stemmer=self.use_stemmer,
        )["rougeL"]


@attrs.define
class BleuMetric(Metric):
    metric: EvaluationModule

    @classmethod
    def setup(cls) -> "BleuMetric":
        bleu = evaluate.load("sacrebleu")
        return cls(metric=bleu)

    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        return (
            self.metric.compute(
                predictions=[prediction.get_text()],
                references=[gt.get_text() for gt in ground_truths],
                use_effective_order=True,
            )["score"]
            / 100
        )


@attrs.define
class MeteorMetric(Metric):
    metric: EvaluationModule

    @classmethod
    def setup(cls) -> "MeteorMetric":
        meteor = evaluate.load("meteor")
        return cls(metric=meteor)

    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        return self.metric.compute(
            predictions=[prediction.get_text()],
            references=[gt.get_text() for gt in ground_truths],
        )["meteor"]


@attrs.define
class BertScoreMetric(Metric):
    metric: EvaluationModule
    model_type: str = "bert-base-uncased"

    @classmethod
    def setup(cls) -> "BertScoreMetric":
        bertscore = evaluate.load("bertscore")
        return cls(metric=bertscore)

    def _calculate(self, ground_truths: list[Data], prediction: Data) -> float:
        return self.metric.compute(
            predictions=[prediction.get_text()],
            references=[gt.get_text() for gt in ground_truths],
            lang="en",
            model_type=self.model_type,
        )["f1"][0]
