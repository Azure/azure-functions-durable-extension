#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import logging
import time
from typing import List

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()


@bp.orchestration_trigger(context_name="context", orchestration="LongRunningOrchestrator")
def long_running_orchestrator(context: df.DurableOrchestrationContext):
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
async def terminate_instance(req: func.HttpRequest, client: df.DurableOrchestrationClient):
    instance_id = req.route_params.get("instanceId") or req.params.get("instanceId")
    reason = "Long-running orchestration was terminated early."
    try:
        await client.terminate(instance_id, reason)
        return func.HttpResponse(status_code=200)
    except Exception as ex:
        return func.HttpResponse(str(ex), status_code=400, mimetype="text/plain")
