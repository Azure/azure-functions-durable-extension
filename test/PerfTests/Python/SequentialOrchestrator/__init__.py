import logging
import json

import azure.functions as func
import azure.durable_functions as df


def orchestrator_function(context: df.DurableOrchestrationContext):
    for num in range(100):
        yield context.call_activity('Hello', str(num))
    return "Done"

main = df.Orchestrator.create(orchestrator_function)