import tempfile

import attrs
import pandas as pd
import pytest
from azure.ai.ml import MLClient
from azure.ai.ml.constants import AssetTypes
from azure.ai.ml.entities import Data
from azureml.fsspec import AzureMachineLearningFileSystem

from medbench.aml import AzureML  # Assuming the class is in a file named aml.py


@pytest.fixture
def aml(mocker):
    mock_client = mocker.MagicMock(spec=MLClient)
    aml = AzureML(client=mock_client)

    mock_get_dataset_latest_version = mocker.patch.object(
        AzureML, "get_dataset_latest_version"
    )
    mock_get_dataset_latest_version.return_value = "1"

    yield aml


@pytest.fixture
def mock_dataset():
    yield Data(
        id="dataset_id",
        name="test_dataset",
        version="1",
        path="path/to/file.csv",
        type=AssetTypes.URI_FILE,
    )


@attrs.define
class TemporaryJsonl:
    """Temporary .jsonl file for testing."""

    source_dir: tempfile.TemporaryDirectory[str] = attrs.field(init=False)
    file_name: str = attrs.field(default="file1.jsonl")
    df: pd.DataFrame = attrs.field(
        default=pd.DataFrame({"col1": [1, 3], "col2": [2, 4]})
    )
    str_: str = attrs.field(default='{"col1": 1, "col2": 2}\n{"col1": 3, "col2": 4}')

    def __enter__(self):
        self.source_dir = tempfile.TemporaryDirectory()
        with open(f"{self.source_dir.name}/{self.file_name}", "w") as f:
            f.write(self.str_)
        return self

    def __exit__(self, *args):
        self.source_dir.cleanup()


def test_get_dataset_uri_file_dataframe(aml, mock_dataset, mocker):
    """Test loading .csv dataset as pd.DataFrame.

    Test case: dataset.type == AssetTypes.URI_FILE
    """

    # Mocks
    aml.client.data.get.return_value = mock_dataset

    mock_read_csv = mocker.patch("medbench.aml.pd.read_csv")
    mock_read_csv.return_value = pd.DataFrame()

    # Run the test
    name_input = "test_dataset"
    version_input = "latest"
    result = aml.get_dataset(name=name_input, version=version_input)

    # Assert the expected behavior
    aml.get_dataset_latest_version.assert_called_once_with(name_input)
    aml.client.data.get.assert_called_once_with(
        name=name_input, version=aml.get_dataset_latest_version.return_value
    )
    mock_read_csv.assert_called_once_with(mock_dataset.path)
    assert isinstance(result, pd.DataFrame)


def test_get_dataset_uri_file_fallback(aml, mock_dataset, mocker):
    """Test loading .csv dataset as pd.DataFrame.

    Test case: dataset.type == AssetTypes.URI_FILE
    """

    # Mocks
    aml.client.data.get.return_value = mock_dataset

    mock_read_csv = mocker.patch("medbench.aml.pd.read_csv")
    mock_read_csv.side_effect = Exception

    # Run the test
    name_input = "test_dataset"
    version_input = "latest"
    result = aml.get_dataset(name=name_input, version=version_input)

    # Assert the expected behavior
    aml.get_dataset_latest_version.assert_called_once_with(name_input)
    aml.client.data.get.assert_called_once_with(
        name=name_input, version=aml.get_dataset_latest_version.return_value
    )
    mock_read_csv.assert_called_once_with(mock_dataset.path)
    assert result == mock_dataset


def test_get_dataset_uri_folder_single_jsonl(aml, mock_dataset, mocker):
    """Test direct load of single jsonl file.

    Test case: dataset.type == AssetTypes.URI_FOLDER and read_folder_jsonl is True.
    """

    with TemporaryJsonl() as temp_jsonl:
        # Mocks
        mock_dataset.type = AssetTypes.URI_FOLDER
        aml.client.data.get.return_value = mock_dataset

        mock_fs = mocker.patch("medbench.aml.AzureMachineLearningFileSystem")
        mock_fs_instance = mocker.MagicMock(spec=AzureMachineLearningFileSystem)
        mock_fs_instance.glob.return_value = [temp_jsonl.file_name]
        mock_fs.return_value = mock_fs_instance

        mock_temp_dir = mocker.patch("medbench.aml.tempfile.TemporaryDirectory")
        mock_temp_dir.return_value.__enter__.return_value = temp_jsonl.source_dir.name

        # Run the test
        result = aml.get_dataset(
            name="test_dataset", version="latest", read_folder_jsonl=True
        )

        # Assert the expected behavior
        # File system is connected to data asset
        mock_fs.assert_called_once_with(mock_dataset.id)
        mock_fs_instance.get.assert_called_once_with(
            rpath=mock_dataset.id, lpath=temp_jsonl.source_dir.name
        )
        assert isinstance(result, pd.DataFrame)
        assert result.to_dict() == temp_jsonl.df.to_dict()


def test_get_dataset_uri_folder_many_jsonl(aml, mock_dataset, mocker):
    """Test direct load of data asset with multiple jsonl files.

    Test case: dataset.type == AssetTypes.URI_FOLDER and read_folder_jsonl is True.
    """

    with TemporaryJsonl() as temp_jsonl:
        # Mocks
        mock_dataset.type = AssetTypes.URI_FOLDER
        aml.client.data.get.return_value = mock_dataset

        mock_fs = mocker.patch("medbench.aml.AzureMachineLearningFileSystem")
        mock_fs_instance = mocker.MagicMock(spec=AzureMachineLearningFileSystem)
        mock_fs_instance.glob.return_value = [temp_jsonl.file_name, "extra.jsonl"]
        mock_fs.return_value = mock_fs_instance

        # Run the test
        result = aml.get_dataset(
            name="test_dataset", version="latest", read_folder_jsonl=True
        )

        # Assert the expected behavior
        # File system is connected to data asset
        mock_fs.assert_called_once_with(mock_dataset.id)
        mock_fs_instance.get.assert_not_called()
        assert result == mock_dataset

def test_get_dataset_uri_folder_many_jsonl_targeted(aml, mock_dataset, mocker):
    """Test direct load of data asset with multiple jsonl files.

    Test case: dataset.type == AssetTypes.URI_FOLDER and read_folder_jsonl is True.
    """

    with TemporaryJsonl() as temp_jsonl:
        # Mocks
        mock_dataset.type = AssetTypes.URI_FOLDER
        aml.client.data.get.return_value = mock_dataset

        mock_fs = mocker.patch("medbench.aml.AzureMachineLearningFileSystem")
        mock_fs_instance = mocker.MagicMock(spec=AzureMachineLearningFileSystem)
        mock_fs_instance.glob.return_value = [temp_jsonl.file_name, "extra.jsonl"]
        mock_fs.return_value = mock_fs_instance

        mock_temp_dir = mocker.patch("medbench.aml.tempfile.TemporaryDirectory")
        mock_temp_dir.return_value.__enter__.return_value = temp_jsonl.source_dir.name

        # Run the test
        result = aml.get_dataset(
            name="test_dataset", version="latest", read_folder_jsonl=True, target_jsonl=temp_jsonl.file_name
        )

        # Assert the expected behavior
        # File system is connected to data asset
        mock_fs.assert_called_once_with(mock_dataset.id)
        mock_fs_instance.get.assert_called_once_with(
            rpath=mock_dataset.id, lpath=temp_jsonl.source_dir.name
        )
        assert isinstance(result, pd.DataFrame)
        assert result.to_dict() == temp_jsonl.df.to_dict()

def test_get_dataset_uri_folder_jonl_not_found(aml, mock_dataset, mocker):
    """Test direct load of data asset with multiple jsonl files.

    Test case: dataset.type == AssetTypes.URI_FOLDER and read_folder_jsonl is True.
    """

    # Mocks
    mock_dataset.type = AssetTypes.URI_FOLDER
    aml.client.data.get.return_value = mock_dataset

    mock_fs = mocker.patch("medbench.aml.AzureMachineLearningFileSystem")
    mock_fs_instance = mocker.MagicMock(spec=AzureMachineLearningFileSystem)
    mock_fs_instance.glob.return_value = ["existing.jsonl"]
    mock_fs.return_value = mock_fs_instance

    # Run the test
    with pytest.raises(ValueError):
        _ = aml.get_dataset(
            name="test_dataset", version="latest", read_folder_jsonl=True, target_jsonl="not_found.jsonl"
        )

def test_get_dataset_uri_folder(aml, mock_dataset):
    """Test loading data asset of folder dataset.

    Test case: dataset.type == AssetTypes.URI_FOLDER and read_folder_jsonl is False.
    """
    # Mocks
    mock_dataset.type = AssetTypes.URI_FOLDER
    aml.client.data.get.return_value = mock_dataset

    # Run the test
    result = aml.get_dataset(
        name="test_dataset", version="latest", read_folder_jsonl=False
    )

    # Assert the expected behavior
    assert result == mock_dataset
