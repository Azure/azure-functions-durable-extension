// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Custom exception types
class ErrorWithCause extends Error {
    constructor(message: string) {
        super(message);
        delete (this as any).message; // Ensure our getter overrides it

        this._message = message; // Store the original message

        // Fix the prototype chain (necessary when extending built-ins in TypeScript)
        Object.setPrototypeOf(this, new.target.prototype);
    }

    cause: Error | undefined;
    _message: string;

    get message(): string {
        let msg = `${this.name}: ${this._message}`;
        if (this.cause) {
            msg += `\nCaused by: ${this.cause.message}`;
        }
        return msg;
    }
}


export class InvalidOperationException extends ErrorWithCause {
    constructor(message: string) {
        super(message);
        this.name = "InvalidOperationException";
    }
}

export class OverflowException extends ErrorWithCause {
    constructor(message: string) {
        super(message);
        this.name = "OverflowException";
    }
}

// Custom exception carrying structured properties, mirroring the .NET isolated
// BusinessValidationException used by CustomExceptionPropertiesInFailureDetails.
// Extends Error directly (not ErrorWithCause) so `message` stays the plain
// exception message ("Business logic validation failed"), matching the .NET
// reference where FailureDetails.ErrorMessage has no type prefix.
export class BusinessValidationException extends Error {
    stringProperty: string;
    intProperty: number;
    longProperty: number;
    dateTimeProperty: string;
    dictionaryProperty: Record<string, unknown>;
    listProperty: unknown[];
    nullProperty: unknown;

    constructor(
        message: string,
        stringProperty: string,
        intProperty: number,
        longProperty: number,
        dateTimeProperty: string,
        dictionaryProperty: Record<string, unknown>,
        listProperty: unknown[],
        nullProperty: unknown
    ) {
        super(message);
        this.name = "BusinessValidationException";
        // Fix the prototype chain (necessary when extending built-ins in TypeScript)
        Object.setPrototypeOf(this, new.target.prototype);
        this.stringProperty = stringProperty;
        this.intProperty = intProperty;
        this.longProperty = longProperty;
        this.dateTimeProperty = dateTimeProperty;
        this.dictionaryProperty = dictionaryProperty;
        this.listProperty = listProperty;
        this.nullProperty = nullProperty;
    }
}
