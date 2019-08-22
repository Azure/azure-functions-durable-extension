﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextInterfaces;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class ClientFunctions
    {
        [NoAutomaticTrigger]
        public static async Task StartFunction(
            [DurableClient] IDurableClient client,
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

        // NOTE: The TestTaskHub app setting name must exist in order for the job host to successfully index this function.
        [NoAutomaticTrigger]
        public static async Task StartFunctionWithTaskHub(
            [DurableClient(TaskHub = "%TestTaskHub%")] IDurableClient client,
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
        public static void GetEntityClient(
            [DurableClient] IDurableEntityClient client,
            EntityId entityId,
            TestEntityClient[] clientRef)
        {
            // Give a client object created via the binding back to the caller
            clientRef[0] = new TestEntityClient(client, entityId);
        }
    }
}
