# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

import logging
import azure.functions as func

import time
from typing import List
from azure.durable_functions import DurableOrchestrationContext, DurableOrchestrationClient
from azure.durable_functions import Blueprint

bp = Blueprint()

@bp.orchestration_trigger(context_name="context", orchestration="LongRunningOrchestrator")
def long_running_orchestrator(context: DurableOrchestrationContext):
    logging.info("Starting long-running orchestration.")
    outputs: List[str] = []
    for _ in range(100000):
        res = yield context.call_activity("simulated_work_activity", 100)
        outputs.append(res)
    return outputs

@bp.activity_trigger(input_name="sleepms")
def simulated_work_activity(sleepms: int) -> str:
    logging.info("Sleeping for %sms.", sleepms)
    time.sleep(sleepms / 1000.0)
    return f"Slept for {sleepms}ms."

@bp.route(route="TerminateInstance", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def terminate_instance(req: func.HttpRequest, client: DurableOrchestrationClient) -> func.HttpResponse:
    instance_id = req.route_params.get("instanceId") or req.params.get("instanceId")
    reason = "Long-running orchestration was terminated early."
    try:
        await client.terminate(instance_id, reason)
        return func.HttpResponse(status_code=200)
    except Exception as ex:
        return func.HttpResponse(str(ex), status_code=400, mimetype="text/plain")