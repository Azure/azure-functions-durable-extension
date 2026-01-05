// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates the Function Chaining pattern. In this pattern, activity functions
 * are called sequentially, where the output of one function can be used as input to the next.
 * This is the most basic Durable Functions pattern and is useful when you need to execute
 * a sequence of operations in a specific order.
 *
 * Pattern documentation:
 * https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-sequence
 *
 * To run this sample:
 *   1. Start the function app locally using `func host start` or run from Visual Studio
 *   2. Make an HTTP POST request to: http://localhost:7071/orchestrators/E1_HelloSequence
 *
 * No special app settings are required for this sample.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        // Orchestrator function that chains multiple activity function calls sequentially.
        // Each activity is awaited before calling the next, demonstrating ordered execution.
        [FunctionName("E1_HelloSequence")]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Call activities sequentially - each call waits for the previous to complete
            outputs.Add(await context.CallActivityAsync<string>("E1_SayHello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("E1_SayHello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("E1_SayHello_DirectInput", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        // Activity function that demonstrates getting input via the activity context.
        // This approach allows access to additional context properties if needed.
        [FunctionName("E1_SayHello")]
        public static string SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $"Hello {name}!";
        }

        // Activity function that demonstrates direct input binding.
        // This is simpler when you only need the input value.
        [FunctionName("E1_SayHello_DirectInput")]
        public static string SayHelloDirectInput([ActivityTrigger] string name)
        {
            return $"Hello {name}!";
        }
    }
}
