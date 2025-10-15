"""AML interface for Healthcare AI Model Evaluator.

For more information about what this module can do, check `notebooks/aml_functionality.ipynb`.
"""

import os
import tempfile
from typing import Iterable

import attrs
import pandas as pd
from azure.ai.ml import MLClient
from azure.ai.ml.constants import AssetTypes
from azure.ai.ml.entities import Data, Registry
from azure.core.exceptions import ResourceNotFoundError
from azure.identity import DefaultAzureCredential
from azureml.fsspec import AzureMachineLearningFileSystem
import logging


@attrs.define
class AzureML:
    """Azure ML connector class.

    This class is a wrapper around the Azure ML SDK with convenience methods for
    registering and retrieving datasets.
    """

    client: MLClient

    @classmethod
    def connect_to_registry(cls, registry_name: str) -> "AzureML":
        """Create an AzureML instance connected to a shared data registry.

        Args:
            registry_name (str): The name of the shared data registry.

        Returns:
            AzureML: An instance of the AzureML class connected to the shared data registry.
        """
        return cls(
            client=MLClient(
                registry_name=registry_name, credential=DefaultAzureCredential()
            )
        )

    @property
    def registries(self) -> Iterable[Registry]:
        """Get all registries available to MLClient.

        Returns:
            Iterable[Registry]: An iterable of all registries available to MLClient.
        """
        return self.client.registries.list()

    def get_dataset(
        self,
        name: str,
        version: str = "latest",
        read_folder_jsonl: bool = False,
        target_jsonl: str | None = None,
    ) -> pd.DataFrame | Data:
        r"""Get dataset by name and version.

        Args:
            name (str): The name of the dataset to retrieve.
            version (str, optional, defaults to "latest"): The version of the dataset
                to retrieve.
            read_folder_jsonl (bool, optional, defaults to False): Whether to try to load the 
                folder's .jsonl file as a pandas DataFrame
                - If is not AssertTypes.URI_FOLDER this is ignored.
                - If there are multiple .jsonl files in the folder, use this in
                conjunction with `target_jsonl` to specify the name of the
                .jsonl file to read.
            target_jsonl (str, optional, defaults to None): The name of the .jsonl file to read
                from the folder.
                - If provided, read this file as a pandas DataFrame.
                - If not provided and there is only one .jsonl file in the folder,
                read that file.
                - If not provided and there are multiple .jsonl files in the folder,
                return the data asset object.

        Returns:
            pandas.DataFrame | Data: The dataset object.
                If the dataset is of type URI_FILE, it returns a pandas DataFrame
                loaded from the CSV file.
                Otherwise, it returns the dataset object.

        Raises:
            azure.core.exceptions.ResourceNotFoundError: If the dataset cannot be found.
            ValueError: If `read_folder_jsonl` is True and `target_jsonl` is not found.
        """
        if version == "latest":
            version = self.get_dataset_latest_version(name)

        dataset = self.client.data.get(name=name, version=version)

        if dataset.type == AssetTypes.URI_FILE:
            try:
                return pd.read_csv(dataset.path)
            except Exception as e:
                logging.warning(
                    f"Could not read dataset {dataset.name} as a pandas DataFrame: {e}"
                    "Returning the dataset object..."
                )
                return dataset
        elif dataset.type == AssetTypes.URI_FOLDER and read_folder_jsonl:
            fs = AzureMachineLearningFileSystem(dataset.id)

            jsonl_paths = fs.glob("*.jsonl")

            if target_jsonl is not None and target_jsonl not in jsonl_paths:
                raise ValueError(
                    f"Could not find {target_jsonl} in {dataset.name}."
                    "Please provide the correct file name."
                )

            if target_jsonl is None:
                if len(jsonl_paths) == 1:
                    target_jsonl = jsonl_paths[0]
                else:
                    logging.warning(
                        f"Found multiple .jsonl files in {dataset.name} and "
                        "`target_jsonl` was not provided."
                        "Please provide it, or process the Data Asset yourself."
                    )
                    return dataset

            with tempfile.TemporaryDirectory() as temp_dir_name:
                logging.debug(
                    f"Downloading {target_jsonl} to temporary directory `{temp_dir_name}`..."
                )
                fs.get(rpath=dataset.id, lpath=temp_dir_name)

                logging.info(f"Reading {target_jsonl} as a pandas DataFrame.")
                with open(f"{temp_dir_name}/{target_jsonl}", "r") as f:
                    jsonl_str = f.read()
                    return pd.read_json(jsonl_str, lines=True)

        return dataset

    def register_df_as_dataset(
        self,
        df: pd.DataFrame,
        dataset_name: str,
        dataset_description: str,
        dataset_version: str | None = None,
    ) -> Data:
        """Register a pandas DataFrame as a dataset in Azure Machine Learning.

        Args:
            df (pd.DataFrame): The pandas DataFrame to register as a dataset.
            dataset_name (str): The name of the dataset to create or update.
            dataset_description (str): A description of the dataset.
            dataset_version (str, optional): The version of the dataset.
                If not provided, we try to determine the next version automatically.

        Raises:
            ValueError: If the dataset version cannot be determined and is not provided manually.

        Returns:
            Data: The dataset object created or updated in Azure Machine Learning
        """
        if dataset_version is None:
            dataset_version = self._get_dataset_next_version(dataset_name)

        # Save df as csv in temp folder using python TempFolder
        with tempfile.TemporaryDirectory() as tmpdirname:
            data_path = f"{tmpdirname}/{dataset_name}.csv"
            df.to_csv(data_path, index=False)
            dataset = Data(
                name=dataset_name,
                version=dataset_version,
                description=dataset_description,
                path=data_path,
                type=AssetTypes.URI_FILE,
            )
            return self.client.data.create_or_update(dataset)

    def register_folder_as_dataset(
        self,
        folder_path: str,
        dataset_name: str,
        dataset_description: str,
        dataset_version: str | None = None,
    ) -> Data:
        """Register a folder as a dataset in Azure Machine Learning.

        Args:
            folder_path (str): The path to the folder to register as a dataset.
            dataset_name (str): The name of the dataset to create or update.
            dataset_description (str): A description of the dataset.
            dataset_version (str, optional): The version of the dataset.
                If not provided, we try to determine the next version automatically.

        Raises:
            ValueError: If the dataset version cannot be determined and is not provided manually.

        Returns:
            Data: The dataset object created or updated in Azure Machine Learning
        """
        if dataset_version is None:
            dataset_version = self._get_dataset_next_version(dataset_name)

        # If path is not a URI, assert it is a folder
        if not folder_path.startswith("https://"):
            assert os.path.isdir(folder_path), f"{folder_path} is not a folder."

        dataset = Data(
            name=dataset_name,
            version=dataset_version,
            description=dataset_description,
            path=folder_path,
            type=AssetTypes.URI_FOLDER,
        )

        return self.client.data.create_or_update(dataset)

    def get_dataset_latest_version(self, dataset_name: str) -> str:
        """Retrieve the latest version of a dataset.

        Args:
            dataset_name (str): The name of the dataset for which to retrieve the latest version.

        Returns:
            str: The latest version of the dataset.

        Raises:
            ResourceNotFoundError: If the dataset with the specified name does not exist.
        """
        return list(self.client.data.list(name=dataset_name))[0].version

    def _get_dataset_next_version(
        self, dataset_name: str, default_first_version: str = "1"
    ) -> str:
        """Determine next version for a dataset.

        Args:
            dataset_name (str): The name of the dataset for which to determine the next version.
            default_first_version (str, optional): The default version to use if the dataset does not exist. Defaults to "1".

        Returns:
            str: The next version of the dataset.

        Raises:
            ValueError: If the latest version cannot be converted to an integer and a version cannot be determined.
        """
        try:
            latest_version = self.get_dataset_latest_version(dataset_name)
        except ResourceNotFoundError:
            logging.info(
                f'Could not find dataset {dataset_name}, creating new with version "{default_first_version}".'
            )
            latest_version = None
        finally:
            try:
                return (
                    str(int(latest_version) + 1)
                    if latest_version is not None
                    else default_first_version
                )
            except ValueError as e:
                raise ValueError(
                    f"Could not determine next version for dataset {dataset_name}:{latest_version}. "
                    "Please provide a version manually."
                ) from e
