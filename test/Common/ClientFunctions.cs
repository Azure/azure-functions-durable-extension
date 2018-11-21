// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class ClientFunctions
    {
        [NoAutomaticTrigger]
        public static async Task StartFunction(
            [OrchestrationClient] DurableOrchestrationClient client,
            string functionName,
            string instanceId,
            object input,
            TestOrchestratorClient[] clientRef)
        {
            DateTime instanceCreationTime = DateTime.UtcNow;

            instanceId = await client.StartNewAsync(functionName, instanceId, input);
            clientRef[0] = new TestOrchestratorClient(
                client,
                functionName,
                instanceId,
                instanceCreationTime);
        }

        [NoAutomaticTrigger]
        public static async Task StartFunctionWithTaskHub(
            [OrchestrationClient(TaskHub = "%TestTaskHub%")] DurableOrchestrationClient client,
            string functionName,
            string instanceId,
            object input,
            TestOrchestratorClient[] clientRef)
        {
            DateTime instanceCreationTime = DateTime.UtcNow;

            instanceId = await client.StartNewAsync(functionName, instanceId, input);
            clientRef[0] = new TestOrchestratorClient(
                client,
                functionName,
                instanceId,
                instanceCreationTime);
        }
    }
}
