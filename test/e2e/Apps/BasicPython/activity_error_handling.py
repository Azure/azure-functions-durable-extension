#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import logging

import azure.durable_functions as df

from custom_exception_types import InvalidOperationException, OverflowException

bp = df.Blueprint()

attempt_count = {}


@bp.orchestration_trigger(context_name="context", orchestration="RethrowActivityException")
def rethrow_activity_exception(context: df.DurableOrchestrationContext):
    yield context.call_activity('raise_exception', context.instance_id)


@bp.orchestration_trigger(context_name="context", orchestration="CatchActivityException")
def catch_activity_exception(context: df.DurableOrchestrationContext):
    try:
        yield context.call_activity('raise_exception', context.instance_id)
    except Exception as e:
        logging.error(f"Caught exception: {e}")
        return f"Caught exception: {e}"
    

@bp.orchestration_trigger(context_name="context", orchestration="RetryActivityFunction")
def retry_activity_function(context: df.DurableOrchestrationContext):
    yield context.call_activity_with_retry('raise_exception', retry_options=df.RetryOptions(
        first_retry_interval_in_milliseconds=5000,
        max_number_of_attempts=3
    ), input_=context.instance_id)
    return "Success"


@bp.orchestration_trigger(context_name="context", orchestration="CustomRetryActivityFunction")
def custom_retry_activity_function(context: df.DurableOrchestrationContext):
    yield context.call_activity_with_retry('raise_complex_exception', retry_options=df.RetryOptions(
        first_retry_interval_in_milliseconds=5000,
        max_number_of_attempts=3
    ), input_=context.instance_id)
    return "Success"


@bp.activity_trigger(input_name="instance")
def raise_exception(instance: str) -> str:
    global attempt_count
    if instance not in attempt_count:
        attempt_count[instance] = 1
        raise InvalidOperationException(f"This activity failed")
    return "This activity succeeded"


@bp.activity_trigger(input_name="instance2")
def raise_complex_exception(instance2: str) -> str:
    global attempt_count
    if instance2 not in attempt_count:
        attempt_count[instance2] = 1
        raise InvalidOperationException(f"This activity failed") from OverflowException(
            "More information about the failure")
    return "This activity succeeded"
