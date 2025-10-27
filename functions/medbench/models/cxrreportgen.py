import json
from typing import Any, Dict, List

import attrs
import logging

from medbench.config import settings
from medbench.datasets import (
    Data,
    EMediaObjectType,
    HighlightedSegment,
    Instance,
    MediaObject,
)

from .aml import AzureMLModel, AzureMLRunner
from .schema import ModelOutput, ModelRegistry


@attrs.define
class CXRReportGenRunner(AzureMLRunner):
    # TODO: Figure out how to support multiple images (bulk inference + multiple images per request)
    def _build_inference_payload(self, instance: Instance) -> Dict[str, Any]:
        images = []
        texts = []
        for media in instance.input.content:
            if media.type == EMediaObjectType.IMAGE:
                if media.data is None:
                    raise ValueError(
                        "Only base64 images are supported by CXRReportGen at the moment."
                    )

                # XXX: Is there a better way to remove the base64 prefix?
                images.append(media.data.split(",")[-1])
            elif media.type == EMediaObjectType.TEXT:
                texts.append(media.data)
            else:
                logging.warning(
                    f"Ignoring unsupported media type for CXRReportGen: {media.type}"
                )

        if len(images) > 1:
            logging.warning(
                f"Only the first image from {instance.id} "
                "will be used for CXRReportGen inference."
            )

        if len(texts) > 1:
            logging.warning(
                f"Only the first text {instance.id} "
                "will be used for CXRReportGen inference."
            )

        if not images:
            raise ValueError("No images provided for CXRReportGen inference.")

        data = [
            images[0],
            texts[0] if texts else "",
        ]
        payload = {
            "input_data": {
                "data": [data],
                "columns": ["frontal_image", "indication"],
                "index": [0],
            }
        }

        return payload

    def _infer(self, instance_id: str, payload: Dict[str, Any]) -> ModelOutput:
        response = self._client.post(self._model_run.model.endpoint, json=payload)
        response.raise_for_status()
        result = response.json()

        transformed_result = self._transform_inference_response(result)

        return ModelOutput(
            input_id=instance_id,
            completions=transformed_result,
            finish_reason="completed",
            error=None,
        )

    def _transform_inference_response(self, response: List[Dict[str, Any]]) -> Data:
        content: List[MediaObject] = []
        for output in json.loads(response[0]["output"]):
            answer, bounding_boxes = output[0], output[1]

            media = MediaObject.from_text(data=answer)

            if bounding_boxes:
                media.highlighted_segments.append(
                    HighlightedSegment(
                        x_min=bounding_boxes[0][0],
                        y_min=bounding_boxes[0][1],
                        x_max=bounding_boxes[0][2],
                        y_max=bounding_boxes[0][3],
                    )
                )

            content.append(media)

        return Data(content=content)


@ModelRegistry.register("cxrreportgen-model", runner=CXRReportGenRunner)
@attrs.define
class CXRReportGenModel(AzureMLModel):
    name: str = attrs.field(default=settings.cxrreportgen_deployment, validator=attrs.validators.instance_of(str))
    endpoint: str = attrs.field(default=settings.cxrreportgen_endpoint, validator=attrs.validators.instance_of(str))
    version: str = attrs.field(default=settings.cxrreportgen_version, validator=attrs.validators.instance_of(str))
    api_key: str = attrs.field(repr=False, default=settings.cxrreportgen_api_key, validator=attrs.validators.instance_of(str))
