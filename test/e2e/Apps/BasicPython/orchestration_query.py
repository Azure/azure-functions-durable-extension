#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import json

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()


@bp.route(route="GetAllInstances", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def get_all_instances(req: func.HttpRequest, client: df.DurableOrchestrationClient):
    try:
        instances = await client.get_status_all()
        # This would not be necessary if we implemnted __str__ for DurableOrchestrationStatus using to_json under the hood
        instances = json.dumps([i.to_json() for i in instances])
        response = func.HttpResponse(
            instances,
            status_code=200,
            mimetype="application/json"
        )
        return response
    except Exception as ex:
        response = func.HttpResponse(
            str(ex),
            status_code=400,
            mimetype="text/plain"
        )
        return response


@bp.route(route="GetRunningInstances", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def get_running_instances(req: func.HttpRequest, client: df.DurableOrchestrationClient):
    try:
        filter_statuses = [
            df.OrchestrationRuntimeStatus.Running,
            df.OrchestrationRuntimeStatus.Pending,
            df.OrchestrationRuntimeStatus.ContinuedAsNew
        ]
        instances = await client.get_status_by(runtime_status=filter_statuses)
        # This would not be necessary if we implemnted __str__ for DurableOrchestrationStatus using to_json under the hood
        instances = json.dumps([i.to_json() for i in instances])
        response = func.HttpResponse(
            instances,
            status_code=200,
            mimetype="application/json"
        )
        return response
    except Exception as ex:
        response = func.HttpResponse(
            str(ex),
            status_code=400,
            mimetype="text/plain"
        )
        return response
