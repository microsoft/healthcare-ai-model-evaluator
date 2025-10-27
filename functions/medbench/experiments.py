import json
import os
from typing import List, Tuple

import attrs

from medbench.datasets import Dataset
from medbench.models import Model, ModelRun, Runner


@attrs.define
class Experiment:
    name: str
    description: str
    models: List[Tuple[Model, Runner]]
    # XXX: Do we want to support multiple datasets?
    # If so, how to structure the code so that we have all permutaions of
    # models and datasets (runners)? Maybe copying the runner instances
    # and changing datasets? What to do with the prompts and other model params?
    dataset: Dataset

    def __attrs_post_init__(self):
        if not self.models:
            raise ValueError("At least one model must be provided for an experiment")
        if not self.dataset:
            raise ValueError("At least one dataset must be provided for an experiment")

        for model, runner in self.models:
            runner.setup(
                ModelRun(
                    id=f"{model.name}-{self.dataset.name}",
                    model=model,
                    dataset=self.dataset,
                )
            )

    def run(self) -> None:
        for _, runner in self.models:
            runner.run()

    def save(self, path: str, overwrite: bool = False) -> None:
        """Save experiment data.

        The experiment is saved in a subdirectory with the same name as the experiment.
        Retulting in the following structure:
        ```
        experiment_name/
            dataset_name/
                data.json
                model_1_name.json
                model_2_name.json
        ```

        Args:
            path (str): Path to directory where experiment data will be saved.
            overwrite (bool, optional): Overwrite the file if it already exists. Defaults to False.
        """

        path = os.path.join(path, self.name)

        if os.path.exists(path) and not overwrite:
            raise FileExistsError(f"Directory already exists at {path}")

        if not os.path.exists(path):
            os.makedirs(path)

        dataset_path = os.path.join(path, self.dataset.name)
        if not os.path.exists(dataset_path):
            os.makedirs(dataset_path)

        with open(os.path.join(dataset_path, "data.json"), "w+") as f:
            f.write(json.dumps(self.dataset.to_json()))

        for model, runner in self.models:
            runner.save(
                # TODO: Though the rest of the code supports running the same model with
                # different configurations, this line does not.
                # We should change the file name to something unique for each model *run*.
                path=os.path.join(dataset_path, f"{model.name}.json"),
                overwrite=overwrite,
                skip_dataset=True,
            )
