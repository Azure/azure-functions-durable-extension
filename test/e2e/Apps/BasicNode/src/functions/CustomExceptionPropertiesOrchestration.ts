// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler } from 'durable-functions';

class BusinessValidationException extends Error {
    StringProperty: string;
    IntProperty: number;
    LongProperty: number;
    DateTimeProperty: Date;
    DictionaryProperty: Record<string, unknown>;
    ListProperty: unknown[];
    NullProperty: unknown;

    constructor(
        message: string,
        stringProperty: string,
        intProperty: number,
        longProperty: number,
        dateTimeProperty: Date,
        dictionaryProperty: Record<string, unknown>,
        listProperty: unknown[],
        nullProperty: unknown
    ) {
        super(message);
        this.name = 'BusinessValidationException';
        Object.setPrototypeOf(this, new.target.prototype);
        this.StringProperty = stringProperty;
        this.IntProperty = intProperty;
        this.LongProperty = longProperty;
        this.DateTimeProperty = dateTimeProperty;
        this.DictionaryProperty = dictionaryProperty;
        this.ListProperty = listProperty;
        this.NullProperty = nullProperty;
    }
}

df.app.setExceptionPropertiesProvider({
    getExceptionProperties(error: unknown): Record<string, unknown> | undefined {
        if (error instanceof BusinessValidationException) {
            return {
                StringProperty: error.StringProperty,
                IntProperty: error.IntProperty,
                LongProperty: error.LongProperty,
                DateTimeProperty: error.DateTimeProperty,
                DictionaryProperty: error.DictionaryProperty,
                ListProperty: error.ListProperty,
                NullProperty: error.NullProperty,
            };
        }
        return undefined;
    },
});

const BusinessActivity: ActivityHandler = () => {
    throw new BusinessValidationException(
        'Business logic validation failed',
        'validation-error-123',
        100,
        999999999,
        new Date(Date.UTC(2025, 9, 15, 14, 30, 0)),
        { error_code: 'VALIDATION_FAILED', retry_count: 3, is_critical: true },
        ['error1', 'error2', 500, null],
        null
    );
};
df.app.activity('BusinessActivity', { handler: BusinessActivity });

const OrchestrationWithCustomException: OrchestrationHandler = function* (context: OrchestrationContext) {
    try {
        yield context.df.callActivity('BusinessActivity');
    } catch (err) {
        if (err instanceof df.TaskFailedError) {
            return err.failureDetails;
        }
        throw err;
    }
    return null;
};
df.app.orchestration('OrchestrationWithCustomException', OrchestrationWithCustomException);
