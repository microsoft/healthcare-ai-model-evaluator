"""Configuration module for the MedBench package."""

import logging
import os
from enum import Enum

import attrs
from dotenv import load_dotenv

from medbench.json import JsonSerializable


class Environment(Enum):
    """Enumeration of the possible environment types."""

    DEVELOPMENT = "development"
    TESTING = "testing"
    STAGING = "staging"
    PRODUCTION = "production"


@attrs.define(kw_only=True)
class Settings(JsonSerializable):
    env: Environment = Environment.DEVELOPMENT

    azure_storage_blob_endpoint: str = None
    azure_storage_connection_string: str = None

    babelbench_aml_workspace_name: str = "hls-bench"

    azure_openai_deployment: str = None
    azure_openai_version: str = None
    azure_openai_endpoint: str = None
    azure_openai_api_key: str = None

    cxrreportgen_deployment: str = "cxrreportgen"
    cxrreportgen_version: str = "4"
    cxrreportgen_endpoint: str = None
    cxrreportgen_api_key: str = None

    @classmethod
    def from_env(cls) -> "Settings":
        kwargs = {}
        missing_fields = []
        for field_name, field in attrs.fields_dict(cls).items():
            env_var = field_name.upper()
            if env_var in os.environ:
                kwargs[field_name] = os.environ[env_var]
            else:
                logging.info(f"Environment variable {env_var} not found.")
                if field.default is attrs.NOTHING:
                    missing_fields.append(field_name)

        if missing_fields:
            raise ValueError(
                (
                    "Could not initialize Settings due to missing "
                    f"environment variables: {missing_fields}"
                )
            )
        return cls.from_json(kwargs)



load_dotenv()

settings = Settings.from_env()
