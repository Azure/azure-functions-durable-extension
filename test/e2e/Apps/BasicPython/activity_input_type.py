# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

import json
import typing
from datetime import timedelta
from azure.durable_functions import DurableOrchestrationContext, Blueprint
from dateutil.parser import parse


bp = Blueprint()

class MyCustomClassToAvoidConflict:
    name: typing.Optional[str]
    age: int
    data: typing.Optional[typing.List[int]]
    duration: timedelta

    def __init__(self, name: typing.Optional[str], age: int, data: typing.Optional[typing.List[int]], duration: timedelta):
        self.name = name
        self.age = age
        self.data = data if data is not None else []
        self.duration = duration

    def __str__(self):
        # Leading 0 before datetime duration is to match the expected output format
        # in the test cases, which expects zero-padded hours in duration
        # This only works because no test case uses a duration with hours > 9
        return f"{{Name: {self.name}, Age: {self.age}, Duration: 0{self.duration}, Data: {self.data}}}"
    
    __repr__ = __str__

    # These methods must be defined to allow serialization and deserialization
    # by the Durable Functions framework
    def to_json(self):
        return json.dumps({
            "Name": self.name,
            "Age": self.age,
            "Data": self.data,
            "Duration": str(self.duration)
        })

    def from_json(data: str):
        data = json.loads(data)
        Name = data.get("Name")
        Age = data.get("Age")
        Data = data.get("Data")
        # Note - this is a buggy implementation when the Days component of the duration
        # is greater than the remaining days in the current month. Thanks Python. Fortunately,
        # this is not a problem for the test cases in this module
        Duration = parse(data.get("Duration")) - parse("0:00:00")
        return MyCustomClassToAvoidConflict(Name, Age, Data, Duration)

def _parse_duration(duration: str) -> timedelta:
    # Azure serializes timedelta as ISO 8601 duration strings, e.g. "PT1H"
    # Python's timedelta can't parse ISO 8601 directly, so we use a simple parser for this test case
    # Only supports hours and minutes for this scenario
    if duration.startswith("PT"):
        duration = duration[2:]
        hours, minutes = 0, 0
        if "H" in duration:
            hours_str, duration = duration.split("H")
            hours = int(hours_str)
        if "M" in duration:
            minutes_str = duration.replace("M", "")
            minutes = int(minutes_str)
        return timedelta(hours=hours, minutes=minutes)
    return timedelta()

@bp.orchestration_trigger(context_name="context", orchestration="ActivityInputTypeOrchestrator")
def activity_input_type_orchestrator(context: DurableOrchestrationContext):
    output = []

    # Test byte array input
    byte_array_input = [1, 2, 3, 4, 5]
    r_1 = yield context.call_activity("byte_array_input", byte_array_input)
    output.append(r_1)

    # Test empty byte array input
    empty_byte_array = []
    r_2 = yield context.call_activity("byte_array_input", empty_byte_array)
    output.append(r_2)

    # Test single byte input
    single_byte_input = 42
    r_3 = yield context.call_activity("single_byte_input", single_byte_input)
    output.append(r_3)

    # Test custom class input
    custom_class_input = MyCustomClassToAvoidConflict("Test", 25, [1, 2, 3], timedelta(hours=1))
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
        MyCustomClassToAvoidConflict("Test1", 25, [1, 2, 3], timedelta(minutes=30)),
        MyCustomClassToAvoidConflict("Test2", 30, [], timedelta(minutes=45))
    ]
    r_7 = yield context.call_activity("custom_class_array_input", complex_input)
    output.append(r_7)

    return output

@bp.activity_trigger(input_name="input")
def byte_array_input(input: typing.Any, context) -> str:
    if not isinstance(input, list) or not all(isinstance(x, int) for x in input):
        return f"Error: Expected byte[] but got {type(input).__name__}"
    return f"Received byte[]: [{', '.join(str(x) for x in input)}]"

@bp.activity_trigger(input_name="input")
def single_byte_input(input: typing.Any, context) -> str:
    if not isinstance(input, int):
        return f"Error: Expected byte but got {type(input).__name__}"
    return f"Received byte: {input}"

@bp.activity_trigger(input_name="input")
def custom_class_input(input: MyCustomClassToAvoidConflict, context) -> str:
    if not isinstance(input, MyCustomClassToAvoidConflict):
        return f"Error: Expected MyCustomClassToAvoidConflict but got {type(input).__name__}"
    data = input.data
    if not isinstance(data, list):
        return f"Error: Expected Data to be byte[] but got {type(data).__name__}"
    return (
        f"Received CustomClass: {input}"
    )

@bp.activity_trigger(input_name="input")
def int_array_input(input: typing.Any, context) -> str:
    if not isinstance(input, list) or not all(isinstance(x, int) for x in input):
        return f"Error: Expected int[] but got {type(input).__name__}"
    return f"Received int[]: [{', '.join(str(x) for x in input)}]"

@bp.activity_trigger(input_name="input")
def string_input(input: typing.Any, context) -> str:
    if not isinstance(input, str):
        return f"Error: Expected string but got {type(input).__name__}"
    return f"Received string: {input}"

@bp.activity_trigger(input_name="input")
def custom_class_array_input(input: typing.List[MyCustomClassToAvoidConflict], context) -> str:
    if not isinstance(input, list):
        return f"Error: Expected MyCustomClassToAvoidConflict[] but got {type(input).__name__}"
    for item in input:
        if not isinstance(item, MyCustomClassToAvoidConflict):
            return f"Error: Expected MyCustomClassToAvoidConflict but got {type(item).__name__}"

    return f"Received CustomClass[]: {input}"
