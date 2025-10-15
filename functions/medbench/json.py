import logging
from datetime import datetime
from enum import Enum
from typing import ClassVar

import attrs

from medbench.register import BaseRegistry


class JsonSerializable:
    def to_json(self, ignore_empty: bool = False) -> dict:
        def filter_func(attr, value):
            return not attr.name.startswith("_") and (
                not ignore_empty or value is not None
            )

        def serializer_func(instance, field, value):
            if isinstance(value, Enum):
                return value.value
            if isinstance(value, RegisteredSerializable):
                return value.to_json()
            return value

        return attrs.asdict(self, filter=filter_func, value_serializer=serializer_func)

    @classmethod
    def from_json(cls, data: dict):
        if data is None:
            return None

        init_args = {}

        # Get the list of class attributes
        class_attrs = cls.__annotations__.keys()
        if attrs.has(cls):
            class_attrs = [field.name for field in attrs.fields(cls)]

        for key, value in data.items():
            if key not in class_attrs:  # Skip keys not in class attributes
                logging.info(f"Skipping unknown attribute for {cls}: {key}")
                continue
            attr_type = cls.__annotations__.get(key)
            if hasattr(attr_type, "from_json"):
                init_args[key] = attr_type.from_json(value)
            elif isinstance(value, list):
                item_type = attr_type.__args__[0]
                init_args[key] = [
                    item_type.from_json(item)
                    if hasattr(item_type, "from_json")
                    else item
                    for item in value
                ]
            elif attr_type == datetime:
                init_args[key] = datetime.fromisoformat(value)
            elif isinstance(attr_type, type):
                init_args[key] = attr_type(value)
            else:
                init_args[key] = value
        return cls(**init_args)


class RegisteredSerializable(JsonSerializable):
    """Base class for registered serializable objects.

    Attributes:
        _registry (BaseRegistry): Registry for the class.
            This is a class attribute that should be set to the registry
            that the class is registered.
    """

    _registry: ClassVar[BaseRegistry]

    def to_json(self, ignore_empty: bool = False) -> dict:
        data = super().to_json(ignore_empty=ignore_empty)
        registrable_data = {
            "type": self._registry.get_registered_name(type(self)),
            "data": data,
        }
        return registrable_data

    @classmethod
    def from_json(cls, data: dict, _bypass: bool = False):
        """Deserialize a registered object from JSON data.

        This function is used normally, just like `JsonSerializable.from_json`.
        Internally, it will lookup the class in the registry based on the
        `data["type"]` key, and then _bypass_ the registry lookup to call
        the desired class' `from_json` method directly.

        Args:
            data (dict): JSON data.
                Registered objects' jsons should have "type" and "data" keys.
                "type" is used to lookup the class in the registry.
                "data" is passed to the class' from_json method.
            _bypass (bool): Bypass the registry lookup.
                This should only be used internally.
                If False, `data["type"]` is used to lookup the class in the registry.
                If True, `data` is passed directly to `JsonSerializable.from_json`.

        """
        if data is None:
            return None

        if _bypass or "type" not in data:
            # For backwards compatibility, if data was 
            # not saved with the specific class type,
            # try deserializing with the base class.
            return super().from_json(data)

        Model = cls._registry.get(data["type"])
        return Model.from_json(data["data"], _bypass=True)
