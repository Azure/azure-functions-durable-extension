// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler, RetryOptions } from 'durable-functions';
import { InvalidOperationException, OverflowException } from '../Shared/ExceptionTypes';

const attemptCount: Record<string, number> = {};

// Orchestration: RethrowActivityException
const RethrowActivityException: OrchestrationHandler = function* (context: OrchestrationContext) {
    yield context.df.callActivity("raise_exception", context.df.instanceId);
};
df.app.orchestration("RethrowActivityException", RethrowActivityException);

// Orchestration: CatchActivityException
const CatchActivityException: OrchestrationHandler = function* (context: OrchestrationContext) {
    try {
        yield context.df.callActivity("raise_exception", context.df.instanceId);
    } catch (e: any) {
        context.error(`Caught exception: ${e}`);
        return `Caught exception: ${e}`;
    }
};
df.app.orchestration("CatchActivityException", CatchActivityException);

// Orchestration: RetryActivityFunction
const RetryActivityFunction: OrchestrationHandler = function* (context: OrchestrationContext) {
    yield context.df.callActivityWithRetry("raise_exception", new RetryOptions(5000, 3), context.df.instanceId);
    return "Success";
};
df.app.orchestration("RetryActivityFunction", RetryActivityFunction);

// Orchestration: CustomRetryActivityFunction
const CustomRetryActivityFunction: OrchestrationHandler = function* (context: OrchestrationContext) {
    yield context.df.callActivityWithRetry("raise_complex_exception", new RetryOptions(5000, 3), context.df.instanceId);
    return "Success";
};
df.app.orchestration("CustomRetryActivityFunction", CustomRetryActivityFunction);

// Activity: raise_exception
const RaiseExceptionActivity: ActivityHandler = (instance: string) => {
    if (!(instance in attemptCount)) {
        attemptCount[instance] = 1;
        throw new InvalidOperationException("This activity failed");
    }
    return "This activity succeeded";
};
df.app.activity("raise_exception", { handler: RaiseExceptionActivity });

// Activity: raise_complex_exception
const RaiseComplexExceptionActivity: ActivityHandler = (instance2: string) => {
    if (!(instance2 in attemptCount)) {
        attemptCount[instance2] = 1;
        const overflow = new OverflowException("Inner exception message");
        const error = new InvalidOperationException("This activity failed\nMore information about the failure");
        error.cause = overflow;
        throw error;
    }
    return "This activity succeeded";
};
df.app.activity("raise_complex_exception", { handler: RaiseComplexExceptionActivity });
