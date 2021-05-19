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
from azure.durable_functions.models.RetryOptions import RetryOptions

def orchestrator_function(context: df.DurableOrchestrationContext):
    num_activities = context.get_input()
    tasks = []

    retry_options = RetryOptions(1000, 3)
    for i in range(num_activities):
        tasks.append(context.call_activity_with_retry("Hello", retry_options, str(i)))
    yield context.task_all(tasks)

    tasks = []
    for _ in range(num_activities):
        tasks.append(context.call_activity("Hello", "Tokyo"))
    yield context.task_all(tasks)
    b = json.dumps(list(map(lambda x: x.to_json, context.actions)))
    return "done"

main = df.Orchestrator.create(orchestrator_function)