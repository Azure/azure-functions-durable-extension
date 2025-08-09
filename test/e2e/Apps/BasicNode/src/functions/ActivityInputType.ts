// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import { OrchestrationHandler, OrchestrationContext, ActivityHandler } from "durable-functions";

// Helper for durations
function parseDuration(duration: string): number {
    // Parses "HH:MM:SS" or "0HH:MM:SS" to milliseconds
    const match = duration.match(/(\d+):(\d+):(\d+)/);
    if (!match) return 0;
    const [, h, m, s] = match.map(Number);
    return ((h || 0) * 3600 + (m || 0) * 60 + (s || 0)) * 1000;
}

class MyCustomClass {
    name?: string;
    age: number;
    data: number[];
    duration: number; // milliseconds

    constructor(name: string | undefined, age: number, data: number[], duration: number) {
        this.name = name;
        this.age = age;
        this.data = data;
        this.duration = duration;
    }

    toString(): string {
        // Leading 0 before duration to match expected output
        const durationStr = `${new Date(this.duration).toISOString().substring(11, 19)}`;
        return `{Name: ${this.name}, Age: ${this.age}, Duration: ${durationStr}, Data: [${Array.from(this.data).join(", ")}]}`;
    }

    // toJSON(): object {
    //     return {
    //         Name: this.name,
    //         Age: this.age,
    //         Data: Array.from(this.data),
    //         Duration: `0${new Date(this.duration).toISOString().substr(11, 8)}`
    //     };
    // }

    static fromJSON(data: any): MyCustomClass {
        return new MyCustomClass(
            data.name,
            data.age,
            Array.from(data.data),
            data.duration
        );
    }
}

// Orchestrator
const ActivityInputTypeOrchestrator: OrchestrationHandler = function* (context: OrchestrationContext) {
    const output: string[] = [];

    // Test byte array input
    const byte_array_input = [1, 2, 3, 4, 5];
    const r_1 = yield context.df.callActivity("byte_array_input", byte_array_input);
    output.push(r_1);

    // Test empty byte array input
    const empty_byte_array: number[] = [];
    const r_2 = yield context.df.callActivity("byte_array_input", empty_byte_array);
    output.push(r_2);

    // Test single byte input
    const single_byte_input = 42;
    const r_3 = yield context.df.callActivity("single_byte_input", single_byte_input);
    output.push(r_3);

    // Test custom class input
    const custom_class_input = new MyCustomClass("Test", 25, [1, 2, 3], 3600000); // 1 hour in ms
    const r_4 = yield context.df.callActivity("custom_class_input", custom_class_input);
    output.push(r_4);

    // Test int array input
    const int_array_input = [1, 2, 3, 4, 5];
    const r_5 = yield context.df.callActivity("int_array_input", int_array_input);
    output.push(r_5);

    // Test string input
    const string_input = "Test string input";
    const r_6 = yield context.df.callActivity("string_input", string_input);
    output.push(r_6);

    // Test array of custom class input
    const complex_input = [
        new MyCustomClass("Test1", 25, [1, 2, 3], 30 * 60 * 1000), // 30 min
        new MyCustomClass("Test2", 30, [], 45 * 60 * 1000) // 45 min
    ];
    const r_7 = yield context.df.callActivity("custom_class_array_input", complex_input);
    output.push(r_7);

    return output;
};
df.app.orchestration("ActivityInputTypeOrchestrator", ActivityInputTypeOrchestrator);

// Activities

const byte_array_input: ActivityHandler = (input: Uint8Array): string => {
    if (!Array.isArray(input) || !input.every(x => typeof x === "number")) {
        return `Error: Expected byte[] but got ${typeof input} ${input}`;
    }
    return `Received byte[]: [${Array.from(new Uint8Array(input)).join(", ")}]`;
};
df.app.activity("byte_array_input", { handler: byte_array_input });

const single_byte_input: ActivityHandler = (input: number): string => {
    if (typeof input !== "number") {
        return `Error: Expected byte but got ${typeof input}`;
    }
    return `Received byte: ${input}`;
};
df.app.activity("single_byte_input", { handler: single_byte_input });

const custom_class_input: ActivityHandler = (input: MyCustomClass): string => {
    const obj = MyCustomClass.fromJSON(input);
    const data = obj.data;
    if (!(Array.isArray(data) && data.every(x => typeof x === "number"))) {
        return `Error: Expected Data to be byte[] but got ${typeof data}`;
    }
    return `Received CustomClass: ${obj.toString()}`;
};
df.app.activity("custom_class_input", { handler: custom_class_input });

const int_array_input: ActivityHandler = (input: number[]): string => {
    if (!Array.isArray(input) || !input.every(x => typeof x === "number")) {
        return `Error: Expected int[] but got ${typeof input}`;
    }
    return `Received int[]: [${input.join(", ")}]`;
};
df.app.activity("int_array_input", { handler: int_array_input });

const string_input: ActivityHandler = (input: string): string => {
    if (typeof input !== "string") {
        return `Error: Expected string but got ${typeof input}`;
    }
    return `Received string: ${input}`;
};
df.app.activity("string_input", { handler: string_input });

const custom_class_array_input: ActivityHandler = (input: any): string => {
    let parsedInput: MyCustomClass[] = [];
    if (!Array.isArray(input)) {
        return `Error: Expected MyCustomClass[] but got ${typeof input}`;
    }
    for (const item of input) {
        if (!(item instanceof MyCustomClass) &&
            !(item && typeof item === "object" && "name" in item && "age" in item && "data" in item && "duration" in item)) {
            return `Error: Expected MyCustomClass but got ${typeof item}`;
        }
        parsedInput.push(item instanceof MyCustomClass ? item : MyCustomClass.fromJSON(item));
    }
    return `Received CustomClass[]: [${Array.from(parsedInput).map(x => x.toString()).join(", ")}]`;
};
df.app.activity("custom_class_array_input", { handler: custom_class_array_input });