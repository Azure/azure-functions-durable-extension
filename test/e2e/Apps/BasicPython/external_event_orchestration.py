#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import http

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()


@bp.orchestration_trigger(context_name="context", orchestration="ExternalEventOrchestrator")
def external_event_orchestrator(context: df.DurableOrchestrationContext) -> str:
    context.wait_for_external_event("Approval")
    return "Orchestrator Finished!"


@bp.route(route="SendExternalEvent_HttpStart", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def send_external_event_http_start(req: func.HttpRequest, client):
    try:
        instance_id = req.get_json()
        if isinstance(instance_id, dict):
            instance_id = instance_id.get("instanceId")
        await client.raise_event(instance_id, "Approval", True)
        return func.HttpResponse(
            f"External event sent to {instance_id}.",
            status_code=http.HTTPStatus.OK
        )
    except Exception as ex:
        # gRPC errors are surfaced as generic exceptions in Python SDK
        return func.HttpResponse(
            f"{type(ex).__name__}: {str(ex)}",
            status_code=http.HTTPStatus.BAD_REQUEST
        )
