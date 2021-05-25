import logging
import json

import azure.functions as func
import azure.durable_functions as df


def orchestrator_function(context: df.DurableOrchestrationContext):
    num_activities = context.get_input()
    tasks = []

    for _ in range(num_activities):
        tasks.append(context.call_sub_orchestrator("SequentialOrchestrator"))
    yield context.task_all(tasks)
    return "done"

main = df.Orchestrator.create(orchestrator_function)