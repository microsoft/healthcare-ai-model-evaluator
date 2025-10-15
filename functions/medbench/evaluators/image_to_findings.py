"""Image to findings evaluators."""

import logging
import pickle
from typing import Protocol

import attrs
import faiss
import numpy as np
import pandas as pd

from medbench.datasets import (
    Data,
    EMediaObjectType,
    Instance,
    MediaObject,
)
from medbench.models import ModelOutput

from .multimodal import MultimodalEvaluatorRunner


class ImageFindingsRetrieverProtocol(Protocol):
    def get_similar_findings(self, image: str) -> list[str]: ...


@attrs.define(kw_only=True)
class ImageFindingsFaissRetriever:
    """Image to findings retriever using Faiss.

    Reimplementation of the image to findings retriever using Faiss from the MI2 example:
    https://github.com/microsoft/healthcareai-examples-pr/blob/95f0765281b9e27a7c71bb4d51d183efbd98ddec/azureml/advanced_demos/image_search/2d_image_search.ipynb
    """

    index: faiss.IndexFlatL2

    @classmethod
    def from_pre_embedded_data(
        cls,
        image_embeddings_df: pd.DataFrame,
        image_embeddings_column: str = "image_features",
    ) -> "ImageFindingsFaissRetriever":
        # Dimension of the feature vectors
        embeddings_dimension = (
            image_embeddings_df[image_embeddings_column].iloc[0].shape[0]
        )
        # Create the index
        index = faiss.IndexFlatL2(embeddings_dimension)
        # Stack the feature vectors
        features = np.stack(image_embeddings_df[image_embeddings_column].values)
        # Add vectors to the index
        index.add(features)
        return cls(index=index)

    @classmethod
    def merge_embeddings_categories(
        cls,
        embeddings_pickle_path: str,
        categories_csv_path: str,
        image_embeddings_column: str = "image_features",
        file_name_column: str = "Name",
    ) -> pd.DataFrame:
        with open(embeddings_pickle_path, "rb") as f:
            results: dict = pickle.load(f)

        file_name_list = []
        image_embeddings_list = []
        for key in results.keys():
            file_name_list.append(key)
            image_embeddings_list.append(
                np.array(results[key][image_embeddings_column][0]).flatten()
            )

        mi2_features_df = pd.DataFrame(
            {
                file_name_column: file_name_list,
                image_embeddings_column: image_embeddings_list,
            }
        )

        train_df = pd.read_csv(categories_csv_path)
        return pd.merge(train_df, mi2_features_df, on=file_name_column, how="inner")

    def get_similar_findings(self, image: str) -> list[str]:
        return ["Finding 1", "Finding 2", "Finding 3"]

    def _faiss_search(self, image_embeddings: np.ndarray, k: int = 3) -> list[int]:
        distances, indices = self.index.search(image_embeddings, k)
        return indices

@attrs.define(kw_only=True)
class SimilarFindingsEvaluatorRunner(MultimodalEvaluatorRunner):
    """Similarity based image-to-findings evaluator.

    This evaluator is inspired by the image search demo using MedImageInsight (MI2):
    https://github.com/microsoft/healthcareai-examples-pr/blob/95f0765281b9e27a7c71bb4d51d183efbd98ddec/azureml/advanced_demos/image_search/2d_image_search.ipynb

    This evaluator retrieves images and labels (findings) similar to the input image.
    It then uses these similar findings to evaluate output of the AI system being evaluated.
    """

    findings_retriever: ImageFindingsRetrieverProtocol

    similar_findings_header: str = "SIMILAR FINDINGS:\n\n"
    similar_findings_prompt: str = """\
Below are the main findings of images similar to the one in the original input:
{similar_findings}
"""

    system_prompt: str = """\
{base_eval_prompt}

This time you will evaluate AI systems responses on the {dataset_name} dataset. \
Below is dataset description:
{dataset_description}

{task_specific_eval}

{output_specs_prompt}
"""

    base_eval_prompt: str = """\
You are an AI assistant with deep expertise in the medical domain. \
You're tasked with evaluating how well other AI systems can provide findings based on medical images.

Independently of the task specifics, for a fair evaluation you shall:
- Evaluate the correctness of facts present in the answer;
- Evaluate relevancy of the given information to the task;
- Ensure the response covers all necessary aspects of the medical query or context;
- Evaluate whether the response includes hallucinated information, that the model could not have inferred from the context;

Ultimately, you should put yourself in the shoes of a medical professional and evaluate the response as if it was given by a human expert. \
You shall judge whether the response, if given in a real case scenario, would be helpful to you (a medical professional) to conduct your work \
in the best way possible.\
"""

    task_specific_eval_prompt: str = """ \
When evaluating whether the findings are relevant and correct, also consider \
the input context that was provided to the AI system being evaluated. \
Can the input context fully support the findings provided by the AI system?

For example, some findings may require multiple images or more information \
about the patient history to be correctly diagnosed.\
"""

    def get_similar_findings(self, image: str) -> list[str]:
        """Retrieve similar findings for an image."""
        return self.findings_retriever.get_similar_findings(image)

    def _prepare_evaluation_instance(
        self, instance: Instance, result: ModelOutput
    ) -> Instance:
        images_content = []
        for media in instance.input.content:
            if media.type == EMediaObjectType.IMAGE:
                images_content.append(media)

        similar_findings = []
        for media in images_content:
            if media.data:
                similar_findings.extend(self.get_similar_findings(media.data))
            else:
                # TODO: Add support
                logging.warning(
                    (
                        "Only base64 images are supported by the "
                        "`SimilarFindingsEvaluatorRunner` at the moment. Skipping image."
                    )
                )

        eval_input: Data = Data(
            content=[
                MediaObject(
                    type=EMediaObjectType.TEXT,
                    data=(
                        self.dataset_input_header
                        + instance.input.get_text()
                        + self.system_output_header
                        + result.completions.get_text()
                        + self.similar_findings_header
                        + self.similar_findings_prompt.format(
                            similar_findings="\n".join(similar_findings)
                        )
                    ),
                )
            ]
            + images_content
        )

        return attrs.evolve(instance, input=eval_input)
