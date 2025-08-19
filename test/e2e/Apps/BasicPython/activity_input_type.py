#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import json
import typing
from datetime import timedelta
from dateutil.parser import parse

import azure.durable_functions as df

bp = df.Blueprint()

# Note: This class is named so as to avoid conflicts with Functions components - CustomClass seems to
# exist in the Azure Functions SDK and was causing issues with serialization/deserialization
# when passing instances of this class as input to activities.


class MyCustomClass:
    name: typing.Optional[str]
    age: int
    data: typing.Optional[bytes]
    duration: timedelta

    def __init__(self, name: typing.Optional[str], age: int, data: typing.Optional[bytes], duration: timedelta):
        self.name = name
        self.age = age
        self.data = data if data is not None else bytes()
        self.duration = duration

    def __str__(self):
        # Leading 0 before datetime duration is to match the expected output format
        # in the test cases, which expects zero-padded hours in duration
        # This only works because no test case uses a duration with hours > 9
        return f"{{Name: {self.name}, Age: {self.age}, Duration: 0{self.duration}, Data: {list(self.data)}}}"

    __repr__ = __str__

    # These methods must be defined to allow serialization and deserialization
    # by the Durable Functions framework
    def to_json(self):
        return json.dumps({
            "Name": self.name,
            "Age": self.age,
            # Because bytes does not expose to_json and from_json methods,
            # we convert it to a list of integers for serialization.
            # See the notes surrounding the byte_array_input activity
            "Data": list(self.data),
            "Duration": str(self.duration)
        })

    def from_json(data: str):
        data = json.loads(data)
        Name = data.get("Name")
        Age = data.get("Age")
        Data = bytes(data.get("Data"))
        # Note - this is a buggy implementation when the Days component of the duration
        # is greater than the remaining days in the current month. Thanks Python. Fortunately,
        # this is not a problem for the test cases in this module
        Duration = parse(data.get("Duration")) - parse("0:00:00")
        return MyCustomClass(Name, Age, Data, Duration)


@bp.orchestration_trigger(context_name="context", orchestration="ActivityInputTypeOrchestrator")
def activity_input_type_orchestrator(context: df.DurableOrchestrationContext):
    output = []

    # Test byte array input
    byte_array_input = bytes([1, 2, 3, 4, 5])
    # Note: Byte arrays are __not__ valid as activity inputs in Python, as they do not expose to_json and from_json methods.
    # This test passes them as int[] instead - we should decide if this constitutes a bug or not, as there are other
    # types affected by this issue (such as custom types like MyCustomClass above that must define to_json and from_json).
    r_1 = yield context.call_activity("byte_array_input", list(byte_array_input))
    output.append(r_1)

    # Test empty byte array input
    empty_byte_array = bytes()
    r_2 = yield context.call_activity("byte_array_input", list(empty_byte_array))
    output.append(r_2)

    # Test single byte input - Note - in Python, there does not exist a single byte type. We will use an int
    # to represent a single byte value, as it is the closest equivalent.
    single_byte_input = 42
    r_3 = yield context.call_activity("single_byte_input", single_byte_input)
    output.append(r_3)

    # Test custom class input
    custom_class_input = MyCustomClass("Test", 25, bytes([1, 2, 3]), timedelta(hours=1))
    r_4 = yield context.call_activity("custom_class_input", custom_class_input)
    output.append(r_4)

    # Test int array input
    int_array_input = [1, 2, 3, 4, 5]
    r_5 = yield context.call_activity("int_array_input", int_array_input)
    output.append(r_5)

    # Test string input
    string_input = "Test string input"
    r_6 = yield context.call_activity("string_input", string_input)
    output.append(r_6)

    # Test array of custom class input
    complex_input = [
        MyCustomClass("Test1", 25, bytes([1, 2, 3]), timedelta(minutes=30)),
        MyCustomClass("Test2", 30, bytes(), timedelta(minutes=45))
    ]
    r_7 = yield context.call_activity("custom_class_array_input", complex_input)
    output.append(r_7)

    return output


@bp.activity_trigger(input_name="input")
def byte_array_input(input) -> str:
    if not isinstance(input, list) or not all(isinstance(x, int) for x in input):
        return f"Error: Expected byte[] but got {type(input).__name__}"
    # Convert list of integers back to bytes - this is superflous but if we decide to change activity input serialization 
    # to support bytes directly, we can cut out this conversion and the check above
    input = bytes(input)
    if not isinstance(input, bytes):
        return f"Error: Expected byte[] but got {type(input).__name__}"
    return f"Received byte[]: {list(input)}"


@bp.activity_trigger(input_name="input")
def single_byte_input(input) -> str:
    if not isinstance(input, int):
        return f"Error: Expected byte but got {type(input).__name__}"
    return f"Received byte: {input}"


@bp.activity_trigger(input_name="input")
def custom_class_input(input: MyCustomClass) -> str:
    if not isinstance(input, MyCustomClass):
        return f"Error: Expected MyCustomClass but got {type(input).__name__}"
    data = input.data
    if not isinstance(data, bytes):
        return f"Error: Expected Data to be byte[] but got {type(data).__name__}"
    return (
        f"Received CustomClass: {input}"
    )


@bp.activity_trigger(input_name="input")
def int_array_input(input) -> str:
    if not isinstance(input, list) or not all(isinstance(x, int) for x in input):
        return f"Error: Expected int[] but got {type(input).__name__}"
    return f"Received int[]: [{', '.join(str(x) for x in input)}]"


@bp.activity_trigger(input_name="input")
def string_input(input) -> str:
    if not isinstance(input, str):
        return f"Error: Expected string but got {type(input).__name__}"
    return f"Received string: {input}"


@bp.activity_trigger(input_name="input")
def custom_class_array_input(input: typing.List[MyCustomClass]) -> str:
    if not isinstance(input, list):
        return f"Error: Expected MyCustomClass[] but got {type(input).__name__}"
    for item in input:
        if not isinstance(item, MyCustomClass):
            return f"Error: Expected MyCustomClass but got {type(item).__name__}"

    return f"Received CustomClass[]: {input}"
