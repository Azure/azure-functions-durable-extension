#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import logging
import time
from datetime import timedelta

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()


@bp.route(route="TimeoutOrchestrator_HttpStart", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def timer_http_start(req: func.HttpRequest, client: df.DurableOrchestrationClient):
    timeoutSeconds = req.params.get("timeoutSeconds")
    if not timeoutSeconds or not str.isnumeric(timeoutSeconds):
        return func.HttpResponse(
            "Please pass a valid timeoutSeconds value in the query string or in the request body.",
            status_code=400
        )
    instance_id = await client.start_new("TimeoutOrchestrator", None, int(timeoutSeconds))
    logging.info(f"Started orchestration with ID = '{instance_id}'.")
    return client.create_check_status_response(req, instance_id)


@bp.orchestration_trigger(context_name="context", orchestration="TimeoutOrchestrator")
def timeout_orchestrator(context: df.DurableOrchestrationContext):
    timeoutSeconds = context.get_input()
    if not timeoutSeconds or not isinstance(timeoutSeconds, int):
        raise "Timeout value is required for this orchestration."
    timeout = timedelta(seconds=timeoutSeconds)
    deadline = context.current_utc_datetime + timeout

    activity_task = context.call_activity("long_activity", context.instance_id)
    timeout_task = context.create_timer(deadline)

    winner = yield context.task_any([activity_task, timeout_task])
    if winner == activity_task:
        timeout_task.cancel()
        return activity_task.result
    else:
        return "The activity function timed out"


@bp.activity_trigger(input_name="instanceid")
def long_activity(instanceid) -> str:
    # The duration of 5 seconds for this activity was chosen because
    # it is long enough to demonstrate both the activity timeout and the
    # activity success case in the tests for activity timeout.
    time.sleep(5)
    return "The activity function completed successfully"
