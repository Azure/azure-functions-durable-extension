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


def orchestrator_function(context: df.DurableOrchestrationContext):
    num_activities = context.get_input()
    tasks = []

    for _ in range(num_activities):
        tasks.append(context.call_sub_orchestrator("ManyInstancesOrchestrator"))
    yield context.task_all(tasks)
    return "done"

main = df.Orchestrator.create(orchestrator_function)