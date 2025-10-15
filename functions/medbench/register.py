import logging
from abc import ABC
from typing import Any, ClassVar, Generic, MutableMapping, Type, TypeVar

T = TypeVar("T")


class BaseRegistry(Generic[T], ABC):
    """Implements the Register pattern for MedBench.

    To create a new register extend from BaseRegister and set the corresponding generic type.
    ```
    class FooRegister(BaseRegister["Foo"]):
    pass

    @FooRegister.register("base-foo")
    class Foo:
        pass
    ```

    You can retrieve the registered assistant class by using the `get` method:
    ```
    BaseFoo = FooRegister.get("base-foo")
    ```
    """

    # Be warned that we should not access "type" class variables while the type is still generic
    # https://github.com/python/mypy/issues/5144#issuecomment-439524567
    _registry: ClassVar[MutableMapping[str, Type[T]]] = dict()  # type: ignore
    _attrs_registry: ClassVar[MutableMapping[str, Any]] = dict()

    def __init_subclass__(cls, **kwargs):
        r"""Set TableType attribute at subclass creation.

        As defined at PEP-487, this function is a hook method that is triggered whenever
        the parent class is being subclassed.

        Specifically for Registers, this method is used as a convenience feature to initialize
        the `_registry` property to a new dictionary. This is done so each subclass has their
        own separate registry.

        Before modifying anything related to this function, make sure to
        experiment and understand the behaviour of this functionality.

        Reference:
        - Understanding __init_subclass__:
            https://stackoverflow.com/q/45400284/7454638
        - PEP-487
            https://peps.python.org/pep-0487/
        - __init_subclass__ documentation
            https://docs.python.org/3/reference/datamodel.html#object.__init_subclass__
        """
        super().__init_subclass__(**kwargs)
        cls._registry = dict()
        cls._attrs_registry = dict()

    @classmethod
    def manual_register(cls, name: str, medbench_class: Type[T], **kwargs) -> Type[T]:
        cls._registry[name] = medbench_class
        for key, value in kwargs.items():
            cls._attrs_registry[f"{name}.{key}"] = value
        return medbench_class

    @classmethod
    def register(cls, name: str, **kwargs):
        """Decorator for registering objects."""

        def _register_decorator(medbench_class: Type[T]) -> Type[T]:
            return cls.manual_register(name, medbench_class, **kwargs)

        return _register_decorator

    @classmethod
    def get(cls, name: str) -> Type[T]:
        """Retrieve a registered class from its name."""

        if name not in cls._registry:
            error_msg = (
                f"Could not find object registered with name '{name}'. "
                f"Available are: {list(cls._registry.keys())} - "
                "Make sure to import plugins (vectorstores, prompts and assistant) before trying to access them. - "
                "Have you configured your custom code as a plugin?"
            )
            logging.error(error_msg)
            raise KeyError(error_msg)

        return cls._registry[name]

    @classmethod
    def get_attr(cls, name: str, attr: str) -> Any:
        """Retrieve a registered class attribute from its name."""
        attr_key = f"{name}.{attr}"
        if attr_key not in cls._attrs_registry:
            error_msg = (
                f"Could not find object registered with name '{attr_key}'. "
                f"Available are: {list(cls._attrs_registry.keys())} - "
                "Make sure to import plugins (vectorstores, prompts and assistant) before trying to access them. - "
                "Have you configured your custom code as a plugin?"
            )
            logging.error(error_msg)
            raise KeyError(error_msg)
        return cls._attrs_registry[attr_key]

    @classmethod
    def get_registered_name(cls, medbench_class: Type[T]) -> str:
        """Retrieve the registered name of a class."""
        for name, registered_class in cls._registry.items():
            if registered_class is medbench_class:
                return name
        raise KeyError(
            f"Could not find class {medbench_class.__name__} in registry. "
            "Make sure the class is registered before trying to retrieve its name."
        )
