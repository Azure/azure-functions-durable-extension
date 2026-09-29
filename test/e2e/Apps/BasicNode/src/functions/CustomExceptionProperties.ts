// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import {
    ActivityHandler,
    ExceptionPropertiesProvider,
    OrchestrationContext,
    OrchestrationHandler,
    TaskFailedError,
} from 'durable-functions';
import { BusinessValidationException } from '../Shared/ExceptionTypes';

// Register a global provider that surfaces custom properties from thrown
// exceptions into FailureDetails.Properties. Mirrors the .NET isolated
// TestExceptionPropertiesProvider used by BasicDotNetIsolated.
const provider: ExceptionPropertiesProvider = {
    getExceptionProperties(error: unknown): Record<string, unknown> | undefined {
        if (error instanceof BusinessValidationException) {
            return {
                StringProperty: error.stringProperty,
                IntProperty: error.intProperty,
                LongProperty: error.longProperty,
                DateTimeProperty: error.dateTimeProperty,
                DictionaryProperty: error.dictionaryProperty,
                ListProperty: error.listProperty,
                NullProperty: error.nullProperty,
            };
        }
        return undefined;
    },
};
df.app.setExceptionPropertiesProvider(provider);

// Activity: BusinessActivity - throws an exception carrying custom properties.
const BusinessActivity: ActivityHandler = (): void => {
    throw new BusinessValidationException(
        "Business logic validation failed",
        "validation-error-123",
        100,
        999999999,
        "2025-10-15T14:30:00.000Z",
        {
            error_code: "VALIDATION_FAILED",
            retry_count: 3,
            is_critical: true,
        },
        ["error1", "error2", 500, null],
        null
    );
};
df.app.activity("BusinessActivity", { handler: BusinessActivity });

// Sub-orchestration: calls the activity and lets the failure (with its
// structured FailureDetails) propagate out so a parent can observe it. Used by
// the nested scenario below.
const BusinessSubOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    yield context.df.callActivity("BusinessActivity");
};
df.app.orchestration("BusinessSubOrchestration", BusinessSubOrchestration);

// Parent orchestration: calls the activity directly, catches the propagated
// TaskFailedError, and returns its FailureDetails. This mirrors the .NET
// isolated OrchestrationWithCustomException reference so the custom-exception-
// properties feature is validated for Node exactly as it works for dotnet
// isolated.
const OrchestrationWithCustomException: OrchestrationHandler = function* (context: OrchestrationContext) {
    try {
        yield context.df.callActivity("BusinessActivity");
    } catch (e: unknown) {
        if (e instanceof TaskFailedError) {
            return e.failureDetails;
        }
        throw e;
    }

    // Should never reach here.
    return null;
};
df.app.orchestration("OrchestrationWithCustomException", OrchestrationWithCustomException);

// Nested scenario: parent orchestration that calls a sub-orchestration (which
// in turn calls the failing activity), catches the propagated TaskFailedError,
// and returns its FailureDetails — exercising FailureDetails propagation across
// a sub-orchestration boundary.
const NestedOrchestrationWithCustomException: OrchestrationHandler = function* (
    context: OrchestrationContext
) {
    try {
        yield context.df.callSubOrchestrator("BusinessSubOrchestration");
    } catch (e: unknown) {
        if (e instanceof TaskFailedError) {
            return e.failureDetails;
        }
        throw e;
    }

    // Should never reach here.
    return null;
};
df.app.orchestration("NestedOrchestrationWithCustomException", NestedOrchestrationWithCustomException);
