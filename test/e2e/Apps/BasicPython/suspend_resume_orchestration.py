
# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

import azure.functions as func

from azure.durable_functions import DurableOrchestrationClient
from azure.functions import HttpRequest, HttpResponse
from azure.durable_functions import Blueprint


bp = Blueprint()

@bp.route(route="SuspendInstance", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def suspend_instance(req: HttpRequest, client: DurableOrchestrationClient) -> HttpResponse:
    instance_id = req.params.get("instanceId")
    suspend_reason = "Suspending the instance for test."
    try:
        await client.suspend(instance_id, suspend_reason)
        return func.HttpResponse(status_code=200)
    except Exception as ex:
        # Simulate RpcException handling and message
        response = func.HttpResponse(
            str(ex),
            status_code=400,
            mimetype="text/plain"
        )
        return response

@bp.route(route="ResumeInstance", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def resume_instance(req: HttpRequest, client: DurableOrchestrationClient) -> HttpResponse:
    instance_id = req.params.get("instanceId")
    resume_reason = "Resuming the instance for test."
    try:
        await client.resume(instance_id, resume_reason)
        return func.HttpResponse(status_code=200)
    except Exception as ex:
        # Simulate RpcException handling and message
        response = func.HttpResponse(
            str(ex),
            status_code=400,
            mimetype="text/plain"
        )
        return response