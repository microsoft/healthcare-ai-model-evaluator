from abc import abstractmethod
from typing import Any, Dict

import attrs
import requests
import logging

from medbench.datasets import Instance

from .schema import Model, ModelOutput, ModelRegistry, ModelRun, Runner


@attrs.define
class AzureMLRunner(Runner):
    _client: requests.Session = attrs.field(init=False)

    def setup(self, model_run: ModelRun) -> None:
        super().setup(model_run)

        assert isinstance(
            model_run.model, AzureMLModel
        ), "Unsupported `Model` class in ModelRun. Model must be an `AzureMLModel` instance."

        self._client = requests.Session()
        self._client.headers.update(
            {
                # TODO: KeyVault key retrieval
                "Authorization": f"Bearer {model_run.model.api_key}",
                "Content-Type": "application/json",
                # TODO: Test if this applies to all AML deployments or not
                "azureml-model-deployment": f"{model_run.model.name}-{model_run.model.version}",
            }
        )

    def run(self) -> None:
        if self._model_run is None:
            raise ValueError(
                "`ModelRun` is not set. Please call `setup` before running this model."
            )
        for instance in self._model_run.dataset.instances:
            try:
                payload = self._build_inference_payload(instance)
                self._model_run.results.append(self._infer(instance.id, payload))
            except Exception as e:
                logging.error(
                    f"Error running {self.__class__} for instance "
                    f"{self._model_run.dataset.name} {instance.id}: {e}"
                )

    @abstractmethod
    def _build_inference_payload(self, instance: Instance) -> Dict[str, Any]:
        raise NotImplementedError

    @abstractmethod
    def _infer(self, instance_id: str, payload: Dict[str, Any]) -> ModelOutput:
        raise NotImplementedError


@ModelRegistry.register("aml-model", runner=AzureMLRunner)
@attrs.define
class AzureMLModel(Model):
    name: str
    endpoint: str
    version: str
    api_key: str = attrs.field(repr=False)
