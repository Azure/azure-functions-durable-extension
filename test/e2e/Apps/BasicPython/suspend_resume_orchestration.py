#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()


@bp.route(route="SuspendInstance", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def suspend_instance(req: func.HttpRequest, client: df.DurableOrchestrationClient):
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
async def resume_instance(req: func.HttpRequest, client: df.DurableOrchestrationClient):
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
