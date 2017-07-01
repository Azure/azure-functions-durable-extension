// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace VSSample
{
    public static class Counter
    {
        [FunctionName("E3_Counter")]
        public static async Task<int> Run(
            [OrchestrationTrigger] DurableOrchestrationContext counterContext,
            TraceWriter log)
        {
            int counterState = counterContext.GetInput<int>();
            log.Info($"Current counter state is {counterState}. Waiting for next operation.");

            string operation = await counterContext.WaitForExternalEvent<string>("operation");
            log.Info($"Received '{operation}' operation.");

            operation = operation?.ToLowerInvariant();
            if (operation == "incr")
            {
                counterState++;
            }
            else if (operation == "decr")
            {
                counterState--;
            }

            if (operation != "end")
            {
                counterContext.ContinueAsNew(counterState);
            }

            return counterState;
        }
    }
}
