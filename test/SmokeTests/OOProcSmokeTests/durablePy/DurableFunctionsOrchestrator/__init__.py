# This function is not intended to be invoked directly. Instead it will be
# triggered by an HTTP starter function.
# Before running this sample, please:
# - create a Durable activity function (default name is "Hello")
# - create a Durable HTTP starter function
# - add azure-functions-durable to requirements.txt
# - run pip install -r requirements.txt

import logging
import json

import azure.functions as func
import azure.durable_functions as df

class City:
  def __init__(self, country, name):
    self.country = country
    self.name = name

def orchestrator_function(context: df.DurableOrchestrationContext):
    result1 = yield context.call_activity('Hello', "Tokyo")
    result2 = yield context.call_activity('Hello', "Seattle")
    result3 = yield context.call_activity('Hello', "London")
    result4 = yield context.call_activity('Print', 123)

    cities = ["Tokyo", "Seattle", "Cairo"]
    result5 = yield context.call_activity("PrintArray", cities)

    city = City("France", "Paris")
    result5 = yield context.call_activity("PrintObject", city)

    return [result1, result2, result3, result4, result5]

main = df.Orchestrator.create(orchestrator_function)