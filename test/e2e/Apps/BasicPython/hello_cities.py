#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

from datetime import datetime
import logging

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()


@bp.route(route="StartOrchestration")
@bp.durable_client_input(client_name="client")
async def http_start(req: func.HttpRequest, client):
    instance_id = await client.start_new(req.params.get('orchestrationName'))

    logging.info(f"Started orchestration with ID = '{instance_id}'.")
    return client.create_check_status_response(req, instance_id)


@bp.route(route="HelloCities_HttpStart_Scheduled")
@bp.durable_client_input(client_name="client")
async def http_start_scheduled(req: func.HttpRequest, client):
    instance_id = await client.start_new('HelloCities', None, req.params.get('ScheduledStartTime'))

    logging.info(f"Started orchestration with ID = '{instance_id}'.")
    return client.create_check_status_response(req, instance_id)


@bp.orchestration_trigger(context_name="context", orchestration="HelloCities")
def hello_cities(context: df.DurableOrchestrationContext):
    scheduled_start_time = context.get_input() or context.current_utc_datetime
    if isinstance(scheduled_start_time, str):
        scheduled_start_time = datetime.fromisoformat(scheduled_start_time)

    if scheduled_start_time > context.current_utc_datetime:
        yield context.create_timer(scheduled_start_time)

    result1 = yield context.call_activity('say_hello', "Tokyo")
    result2 = yield context.call_activity('say_hello', "Seattle")
    result3 = yield context.call_activity('say_hello', "London")
    return [result1, result2, result3]


@bp.activity_trigger(input_name="city")
def say_hello(city: str) -> str:
    logging.info(f"Saying hello to {city}.")
    return f"Hello {city}!"
