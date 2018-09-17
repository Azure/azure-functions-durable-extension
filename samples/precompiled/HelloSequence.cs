// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName("E1_HelloSequence")]
        public static async Task<string> Run(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {

            var output1 = await context.CallActivityAsync<string>("E1_SayHello", "Tokyo");
            var output2 = await context.CallActivityAsync<string>("E1_SayHelloPlusSeattle", output1);
            var output3 = await context.CallActivityAsync<string>("E1_SayHelloPlusLondon", output2);

            // returns "Hello Tokyo and Seattle and London!"
            return output3;
        }

        [FunctionName("E1_SayHello")]
        public static string SayHello([ActivityTrigger] string input)
        {
            return $"Hello {input}";
        }

        [FunctionName("E1_SayHelloPlusSeattle")]
        public static string SayHelloPlusSeattle([ActivityTrigger] string input)
        {
            return $"{input} and Seattle";
        }

        [FunctionName("E1_SayHelloPlusLondon")]
        public static string SayHelloPlusLondon([ActivityTrigger] string input)
        {
            return $"{input} and London!";
        }
    }
 }
