#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import azure.durable_functions as df

bp = df.Blueprint()


# Disabled at runtime via the AzureWebJobs.DisabledActivity.Disabled app setting in
# local.settings.json. Used by DisabledOrchestrationTests to validate that scheduling a
# disabled-but-still-deployed activity fails the orchestration gracefully instead of poison-looping.
@bp.activity_trigger(input_name="input")
def DisabledActivity(input: str) -> str:
    return input


# Disabled at runtime via the AzureWebJobs.DisabledEntity.Disabled app setting in
# local.settings.json. Used by DisabledOrchestrationTests to validate that calling an operation on a
# disabled-but-still-deployed entity fails the orchestration gracefully instead of poison-looping.
@bp.entity_trigger(context_name="context")
def DisabledEntity(context):
    context.set_result(None)


# Orchestrator that schedules the disabled-but-still-deployed DisabledActivity. Because the activity
# has no active listener, the dispatch must fail the orchestration deterministically instead of
# poison-looping forever. See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
@bp.orchestration_trigger(context_name="context", orchestration="CallDisabledActivity")
def call_disabled_activity(context: df.DurableOrchestrationContext):
    result = yield context.call_activity("DisabledActivity", "hello")
    return result


# Companion orchestrator for the entity dispatch path: calling an operation on the disabled-but-still-
# deployed DisabledEntity must fail the orchestration deterministically rather than poison-looping.
@bp.orchestration_trigger(context_name="context", orchestration="CallDisabledEntity")
def call_disabled_entity(context: df.DurableOrchestrationContext):
    entityId = df.EntityId("DisabledEntity", "disabled-key")
    _ = yield context.call_entity(entityId, "someOperation")
    return "should not reach here"
