import logging
import time
from typing import Any, Dict, List, Optional

import attrs
from openai import AzureOpenAI, BadRequestError, RateLimitError

from medbench.config import settings
from medbench.datasets import CORRECT_TAG, Data, EMediaObjectType, Instance

from .schema import ModelOutput, ModelRegistry, ModelRun, Runner, SystemPromptModel


@attrs.define
class AzureOpenAIRunner(Runner):
    _client: AzureOpenAI = attrs.field(init=False)
    max_retries: int = 3

    @property
    def model(self) -> "BaseOpenAIModel":
        return super().model

    def setup(self, model_run: ModelRun) -> None:
        super().setup(model_run)

        assert isinstance(
            self.model, BaseOpenAIModel
        ), "Unsuported `Model` class in ModelRun. Model must be an `BaseOpenAIModel` instance."

        self._client = AzureOpenAI(
            azure_endpoint=self.model.endpoint,
            azure_deployment=self.model.name,
            api_key=self.model.api_key,
            api_version=self.model.version,
        )

    def run(self) -> None:
        if self._model_run is None:
            raise ValueError(
                "`ModelRun` is not set. Please call `setup` before running this model."
            )
        for instance in self._model_run.dataset.instances:
            messages = self._build_chat_prompt(instance)
            self._model_run.results.append(self._chat(instance.id, messages))

    def build_system_input(self) -> str:
        return {
            "role": "system",
            "content": [
                {
                    "type": "text",
                    "text": self.model.system_prompt,
                }
            ],
        }

    def build_user_input(self, instance: Instance) -> str:
        user_input = {"role": "user", "content": []}
        for media in instance.input.content:
            if media.type == EMediaObjectType.IMAGE:
                if not self.model.vision_enabled:
                    raise ValueError(
                        f"Vision is not enabled for `{self.model.name}` "
                        "model. Only text inputs are supported."
                    )

                user_input["content"].append(
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": media.data or media.location,
                        },
                    }
                )
            elif media.type == EMediaObjectType.TEXT:
                user_input["content"].append(
                    {
                        "type": "text",
                        "text": media.data,
                    }
                )

        if self._model_run.dataset.include_references and instance.references:
            choices: str = ""
            for ref in instance.references:
                ref_content = "".join(
                    [
                        media.data
                        for media in ref.output.content
                        if media.type == EMediaObjectType.TEXT
                    ]
                )

                if (
                    self.is_eval
                    and ref_content
                    and ref.tags
                    and CORRECT_TAG in ref.tags
                ):
                    choices += "**EXPECTED CORRECT ANSWER**"
                choices += f"{ref_content}\n"

            user_input["content"].append(
                {
                    "type": "text",
                    "text": choices,
                }
            )

        return user_input

    def _build_chat_prompt(self, instance: Instance) -> List[Dict[str, Any]]:
        system_prompt = self.build_system_input()
        user_input = self.build_user_input(instance)

        chat_prompt = [
            system_prompt,
            user_input,
        ]

        return chat_prompt

    def _chat(self, instance_id: str, messages: List[Dict[str, Any]]) -> ModelOutput:
        retries = self.max_retries

        completions_create_kwargs = {
            "model": self.model.name,
            "messages": messages,
            "max_completion_tokens": self.model.max_tokens,
            "stop": self.model.stop,
            "stream": self.model.stream,
        }
        if hasattr(self.model, "temperature"):
            completions_create_kwargs["temperature"] = self.model.temperature
        if hasattr(self.model, "top_p"):
            completions_create_kwargs["top_p"] = self.model.top_p
        if hasattr(self.model, "frequency_penalty"):
            completions_create_kwargs["frequency_penalty"] = (
                self.model.frequency_penalty
            )
        if hasattr(self.model, "presence_penalty"):
            completions_create_kwargs["presence_penalty"] = self.model.presence_penalty

        for attempt in range(retries):
            try:
                completion = self._client.chat.completions.create(
                    **completions_create_kwargs
                )

                completion_data: Data | None = None
                if completion.choices[0].message.content:
                    completion_data = Data.from_text(
                        data=completion.choices[0].message.content
                    )

                return ModelOutput(
                    input_id=instance_id,
                    completions=completion_data,
                    finish_reason=completion.choices[0].finish_reason,
                    error=None
                    if completion_data is not None
                    else f"The model did not generate any token. Finish reason: {completion.choices[0].finish_reason}",
                )
            except RateLimitError as e:
                logging.debug(f"Failed to complete instance {instance_id}. Error: {e}")

                if attempt < retries - 1:
                    time.sleep(60)
                else:
                    return ModelOutput(
                        input_id=instance_id,
                        completions=None,
                        finish_reason="error",
                        error=str(e),
                    )
            except BadRequestError as e:
                logging.error(f"Failed to complete instance {instance_id}. Error: {e}")
                return ModelOutput(
                    input_id=instance_id,
                    completions=None,
                    finish_reason="error",
                    error=str(e),
                )
            except Exception as e:
                logging.error(
                    (
                        f"Failed to complete instance {instance_id}. Error: {e} "
                        f"{completion=} "
                        f"{type(e)=}"
                    )
                )
                return ModelOutput(
                    input_id=instance_id,
                    completions=None,
                    finish_reason="error",
                    error=str(e),
                )


@ModelRegistry.register("openai-base-model", runner=AzureOpenAIRunner)
@attrs.define(kw_only=True)
class BaseOpenAIModel(SystemPromptModel):
    name: str = attrs.field(default=settings.azure_openai_deployment, validator=attrs.validators.instance_of(str))
    version: str = attrs.field(default=settings.azure_openai_version, validator=attrs.validators.instance_of(str))
    endpoint: str = attrs.field(default=settings.azure_openai_endpoint, validator=attrs.validators.instance_of(str))
    api_key: str = attrs.field(repr=False, default=settings.azure_openai_api_key, validator=attrs.validators.instance_of(str))

    system_prompt: str

    vision_enabled: bool = False

    max_tokens: int
    stop: Optional[str] = None
    stream: bool = False


@ModelRegistry.register("openai-chat-model", runner=AzureOpenAIRunner)
@attrs.define(kw_only=True)
class OpenAIChatModel(BaseOpenAIModel):
    """OpenAI Chat Model.

    https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/chatgpt?tabs=python-
    """

    temperature: float
    top_p: float
    frequency_penalty: Optional[float] = None
    presence_penalty: Optional[float] = None


@ModelRegistry.register("openai-reasoning-model", runner=AzureOpenAIRunner)
@attrs.define(kw_only=True)
class OpenAIReasoningModel(BaseOpenAIModel):
    """OpenAI Reasoning Model.

    https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/reasoning?tabs=python-secure
    """

    pass
