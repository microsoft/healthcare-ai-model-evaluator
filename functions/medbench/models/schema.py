import json
import os
from abc import ABC, abstractmethod
from typing import ClassVar, List, Optional, Self, Type

import attrs

from medbench.datasets import Data, Dataset
from medbench.json import JsonSerializable, RegisteredSerializable
from medbench.register import BaseRegistry


class ModelRegistry(BaseRegistry["Model"]):
    @classmethod
    def register(cls, name: str, runner: Type["Runner"], **kwargs):
        """Decorator for registering MedBench models."""
        kwargs["runner"] = runner
        return super().register(name, **kwargs)

    @classmethod
    def get_runner(cls, name: str) -> Type["Runner"]:
        return super().get_attr(name, "runner")


@attrs.define
class Model(RegisteredSerializable):
    _registry: ClassVar[ModelRegistry] = ModelRegistry

    name: str
    version: str

    def evolve(self, **kwargs) -> Self:
        """Evolve current instance.

        Given attributes will take precedence over current attributes.
        """
        return attrs.evolve(self, **kwargs)


@attrs.define
class SystemPromptModel(Model):
    system_prompt: str


@attrs.define
class ModelOutput(JsonSerializable):
    input_id: str
    completions: Data
    finish_reason: Optional[str] = None
    error: Optional[str] = None
    metadata: Optional[dict] = None


@attrs.define
class ModelRun(JsonSerializable):
    id: str
    model: Model
    dataset: Dataset
    results: List[ModelOutput] = attrs.field(factory=list)


@attrs.define(kw_only=True)
class Runner(ABC):
    _model_run: ModelRun = attrs.field(init=False, default=None)

    is_eval: bool = False

    @property
    def model(self) -> Model:
        return self._model_run.model

    def setup(self, model_run: ModelRun) -> None:
        self._model_run = model_run

    @abstractmethod
    def run(self) -> None:
        pass

    def save(
        self, path: str, overwrite: bool = False, skip_dataset: bool = False
    ) -> None:
        if os.path.exists(path) and not overwrite:
            raise FileExistsError(f"File already exists at {path}")
        with open(path, "w+") as f:
            run_json = self._model_run.to_json()
            if skip_dataset:
                run_json.pop("dataset")
            f.write(json.dumps(run_json))

    @classmethod
    def load(cls, path: str) -> "Runner":
        with open(path, "r") as f:
            model_run = ModelRun.from_json(json.load(f))

        self = cls()
        self.setup(model_run)
        return self
