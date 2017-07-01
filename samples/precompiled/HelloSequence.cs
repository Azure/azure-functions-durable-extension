// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName("E1_HelloSequence")]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            outputs.Add(await context.CallFunctionAsync<string>("E1_SayHello", "Tokyo"));
            outputs.Add(await context.CallFunctionAsync<string>("E1_SayHello", "Seattle"));
            outputs.Add(await context.CallFunctionAsync<string>("E1_SayHello", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("E1_SayHello")]
        public static string SayHello(
            [ActivityTrigger] DurableActivityContext helloContext)
        {
            string name = helloContext.GetInput<string>();
            return $"Hello {name}!";
        }
    }
 }
