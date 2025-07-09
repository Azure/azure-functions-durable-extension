import json
from azure.durable_functions import DurableOrchestrationClient
from azure.durable_functions.models import OrchestrationRuntimeStatus
from azure.functions import HttpRequest, HttpResponse
import logging

# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()

@bp.route(route="GetAllInstances", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def get_all_instances(req: HttpRequest, client: DurableOrchestrationClient) -> HttpResponse:
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
async def get_running_instances(req: HttpRequest, client: DurableOrchestrationClient) -> HttpResponse:
    try:
        filter_statuses = [
            OrchestrationRuntimeStatus.Running,
            OrchestrationRuntimeStatus.Pending,
            OrchestrationRuntimeStatus.ContinuedAsNew
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