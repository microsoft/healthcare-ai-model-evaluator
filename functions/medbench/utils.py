from typing import Any, Dict, List

from medbench.datasets import Data, Dataset, Instance
from medbench.models import Model, ModelOutput, ModelRun


def load_arena_data(
    dataset_name: str,
    dataset_description: str,
    data_split: str,
    data: List[Dict[str, Any]],
    data_key: str,
    output_keys: List[str],
    max_instances: int = None,
) -> Dict[str, ModelRun]:
    """Load arena style data as ModelRuns.
    
    This function takes a jsonl style dictionary (list of dictionaries)
    and converts into separate ModelRuns for each output key.
    """
    model_runs = {}
    dataset = Dataset(
        name=dataset_name,
        description=dataset_description,
        instances=[],
    )
    for id, instance_data in enumerate(data):
        if max_instances is not None and id >= max_instances:
            break

        instance = Instance(
            id=id,
            input=Data.from_text(data=instance_data[data_key]),
            split=data_split,
            references=[],
        )
        dataset.instances.append(instance)

        for output_key in output_keys:
            if output_key not in model_runs:
                model_runs[output_key] = ModelRun(
                    id=output_key,
                    model=Model(name=output_key, version="0"),
                    dataset=dataset,
                    results=[],
                )

            model_runs[output_key].results.append(
                ModelOutput(
                    input_id=id,
                    completions=Data.from_text(data=instance_data[output_key]),
                )
            )

    return model_runs
