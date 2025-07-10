#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import azure.durable_functions as df

from custom_exception_types import InvalidOperationException, OverflowException

bp = df.Blueprint()

attempt_count = {}


@bp.orchestration_trigger(context_name="context", orchestration="ThrowEntityOrchestration")
def rethrow_entity_exception(context: df.DurableOrchestrationContext):
    entityId = df.EntityId("Counter", "myCounter")
    _ = yield context.call_entity(entityId, "get", context.instance_id)
    return "Success"


@bp.orchestration_trigger(context_name="context", orchestration="CatchEntityOrchestration")
def catch_entity_exception(context: df.DurableOrchestrationContext):
    entityId = df.EntityId("Counter", "myCounter")
    try:
        _ = yield context.call_entity(entityId, "get", context.instance_id)
        return "Success"
    except Exception as e:
        return str(e)


@bp.orchestration_trigger(context_name="context", orchestration="RetryEntityOrchestration")
def retry_entity_function(context: df.DurableOrchestrationContext):
    entityId = df.EntityId("Counter", "myCounter")
    try:
        _ = yield context.call_entity(entityId, "get", context.instance_id)
        return "Success"
    except Exception as e:
        _ = yield context.call_entity(entityId, "get", context.instance_id)
        return "Success"


@bp.entity_trigger(context_name="context")
def Counter(context):
    global attempt_count
    instance_id = context.get_input()
    if not instance_id:
        raise "Did not get a valid instanceId as input to the entity"
    if instance_id not in attempt_count:
        attempt_count[instance_id] = 1
        raise InvalidOperationException(
            "This entity failed\r\nMore information about the failure") from OverflowException("Inner exception message")
    attempt_count[instance_id] += 1
    context.set_result(0)
