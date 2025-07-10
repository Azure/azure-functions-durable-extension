#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import json
import logging

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()


def generate_large_string(size_in_kb: int) -> str:
    return 'A' * (size_in_kb * 1024)


@bp.orchestration_trigger(context_name="context", orchestration="LargeOutputOrchestrator")
def large_output_orchestrator(context: df.DurableOrchestrationContext):
    size_in_kb = context.get_input()
    logging.info("Saying hello.")
    outputs = []
    r_1 = yield context.call_activity("large_output_say_hello", "Tokyo")
    outputs.append(r_1)
    outputs.append(generate_large_string(size_in_kb))
    return outputs


@bp.activity_trigger(input_name="name")
def large_output_say_hello(name) -> str:
    return f"Hello {name}!"


@bp.route(route="LargeOutputOrchestrator_HttpStart", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def start_large_output_orchestrator(req: func.HttpRequest, client):
    logger = logging.getLogger("LargeOutputOrchestrator_HttpStart")
    try:
        size_in_kb = int(req.get_json())
    except Exception:
        size_in_kb = int(req.params.get("sizeInKB", "0"))
    instance_id = await client.start_new("LargeOutputOrchestrator", None, size_in_kb)
    logger.info(f"Started orchestration with ID = '{instance_id}'.")
    return client.create_check_status_response(req, instance_id)


@bp.route(route="LargeOutputOrchestrator_Query_Output", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def query_large_output(req: func.HttpRequest, client):
    id = req.route_params.get("id") or req.params.get("id")
    metadata = await client.get_status(id, show_input=True)
    if metadata is None:
        return func.HttpResponse("Orchestration metadata not found.", status_code=404)
    output = metadata.output
    return func.HttpResponse(
        json.dumps(output),
        status_code=200,
        mimetype="application/json"
    )
