import pandas as pd

from .schema import (
    CORRECT_TAG,
    TRAIN_SPLIT,
    Data,
    Dataset,
    Instance,
    MediaObject,
    Reference,
)


def get_vqarad_dataset(vqarad_df: pd.DataFrame, description: str) -> Dataset:
    instances = []
    for _, row in vqarad_df.iterrows():
        instances.append(
            Instance(
                id=row["qid"],
                input=Data(
                    content=[
                        MediaObject.from_text(
                            data=row["question_rephrase"]
                            if row["question_rephrase"] != "NULL"
                            else row["question"],
                        ),
                        MediaObject.from_image(
                            data=f"data:image/png;base64,{row['data']}",
                            metadata={
                                "name": row["image_name"],
                                "organ": row["image_organ"],
                            },
                        ),
                    ],
                ),
                references=[
                    Reference(
                        output=Data.from_text(data=row["answer"]),
                        tags=[CORRECT_TAG],
                    )
                ],
                split=TRAIN_SPLIT,
                metadata={
                    "phrase_type": row["phrase_type"],
                },
            )
        )

    return Dataset(name="vqarad", description=description, instances=instances)


def get_vqarad_findings_dataset(vqarad_df: pd.DataFrame, description: str) -> Dataset:
    instances = []
    for _, row in vqarad_df.iterrows():
        instances.append(
            Instance(
                id=row["qid"],
                input=Data(
                    content=[
                        MediaObject.from_text(
                            data="Generage findings report.",
                        ),
                        MediaObject.from_image(
                            data=f"data:image/png;base64,{row['data']}",
                            metadata={
                                "name": row["image_name"],
                                "organ": row["image_organ"],
                            },
                        ),
                    ],
                ),
                references=[],
                split=TRAIN_SPLIT,
            )
        )

    return Dataset(name="vqarad_findings", description=description, instances=instances)
