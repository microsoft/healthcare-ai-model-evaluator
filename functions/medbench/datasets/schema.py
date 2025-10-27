from enum import Enum
from typing import List, Optional

import attrs

from medbench.json import JsonSerializable

CORRECT_TAG = "Correct"
TRAIN_SPLIT = "Train"
VAL_SPLIT = "Validation"
TEST_SPLIT = "Test"


class EMediaObjectType(Enum):
    IMAGE = "Image"
    AUDIO = "Audio"
    VIDEO = "Video"
    TEXT = "Text"


@attrs.define
class HighlightedSegment(JsonSerializable):
    x_min: float
    x_max: float
    y_min: Optional[float] = None
    y_max: Optional[float] = None
    z_min: Optional[float] = None
    z_max: Optional[float] = None


@attrs.define
class MediaObject(JsonSerializable):
    type: EMediaObjectType
    data: Optional[str] = None
    location: Optional[str] = None
    metadata: Optional[dict] = None
    highlighted_segments: List[HighlightedSegment] = attrs.field(factory=list)

    def __attrs_post_init__(self):
        if self.data is None and self.location is None:
            raise ValueError(
                "Either `data` or `location` must be provided for a MediaObject"
            )

    @classmethod
    def from_text(cls, **kwargs) -> "MediaObject":
        kwargs.pop("type", None)
        return cls(type=EMediaObjectType.TEXT, **kwargs)

    @classmethod
    def from_image(cls, **kwargs) -> "MediaObject":
        kwargs.pop("type", None)
        return cls(type=EMediaObjectType.IMAGE, **kwargs)


@attrs.define
class Data(JsonSerializable):
    content: List[MediaObject]

    @classmethod
    def from_text(cls, **kwargs) -> "Data":
        return cls(
            content=[
                MediaObject.from_text(**kwargs),
            ]
        )

    @classmethod
    def from_image(cls, **kwargs) -> "Data":
        return cls(
            content=[
                MediaObject.from_image(**kwargs),
            ]
        )

    def get_text(self) -> str:
        return " ".join(
            [
                media.data
                for media in self.content
                if media.type == EMediaObjectType.TEXT
            ]
        )


@attrs.define
class Reference(JsonSerializable):
    """Reference data class.

    This represents the possible output, or expected output of a model.

    For multiple-choice Q&A tasks, each reference may represent a possible answer,
    while only some are marked as correct.

    For summarization tasks, each reference may represent a possible gold standard
    summary of the input.

    Attributes:
        output (Data): The reference output.
        tags (List[str]): Optional tags for the reference.
    """

    output: Data
    tags: Optional[List[str]] = None


@attrs.define
class Instance(JsonSerializable):
    id: str
    input: Data
    references: List[Reference]
    split: str
    sub_split: Optional[str] = None
    perturbation: Optional[str] = None
    metadata: Optional[dict] = None

    def get_ground_truths(self) -> List[Data]:
        return [
            reference.output
            for reference in self.references
            if reference.tags and CORRECT_TAG in reference.tags
        ]


@attrs.define
class Dataset(JsonSerializable):
    """Dataset data class.

    Attributes:
        name (str): Name of the dataset.
        instances (list of Instance): List of instances in the dataset.
            Each instance must have an unique id.
    """

    name: str
    description: str
    instances: List[Instance] = attrs.field(
        validator=attrs.validators.deep_iterable(
            member_validator=attrs.validators.instance_of(Instance),
            iterable_validator=attrs.validators.and_(
                attrs.validators.instance_of(list),
                lambda _, __, value: len(value)
                == len(set(instance.id for instance in value)),
            ),
        )
    )
    include_references: bool = False
