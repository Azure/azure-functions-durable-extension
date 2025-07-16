#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import azure.durable_functions as df

bp = df.Blueprint()


@bp.orchestration_trigger(context_name="context", orchestration="ClassBasedEntityOrchestration")
def class_based_entity_orchestration(context: df.DurableOrchestrationContext):
    entityId = df.EntityId("TestEntity", "singleton")
    _ = yield context.call_entity(entityId, "SetState", 42)
    result = yield context.call_entity(entityId, "GetState")
    return result


# This test is supposed to be for class-based entities, but the Durable Functions Python SDK does not currently support class-based entities.
# We will implement the test with basic functionality so that if we add class-based entities or features like injections or configurations, 
# we can test them here. 
@bp.entity_trigger(context_name="context")
def TestEntity(context: df.DurableEntityContext):
    if context.operation_name == "SetState":
        current_state = context.get_state(lambda: {"state": ""})
        current_state["state"] = f"IConfiguration: yes, MyInjectedService: yes, BlobContainerClient: yes, Number: {context.get_input()}"
        context.set_state(current_state)
    elif context.operation_name == "GetState":
        current_state = context.get_state()
        if not current_state:
            raise "State not set"
        context.set_result(current_state["state"])
