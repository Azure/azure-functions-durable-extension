// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class DurableTaskHostExtensions
    {
        public static async Task<TestDurableClient> StartOrchestratorAsync(
            this ITestHost host,
            string functionName,
            object input,
            ITestOutputHelper output,
            string instanceId = null,
            bool useTaskHubFromAppSettings = false)
        {
            var startFunction = useTaskHubFromAppSettings ?
                typeof(ClientFunctions).GetMethod(nameof(ClientFunctions.StartFunctionWithTaskHub)) :
                typeof(ClientFunctions).GetMethod(nameof(ClientFunctions.StartFunction));

            var clientRef = new TestDurableClient[1];
            var args = new Dictionary<string, object>
            {
                { "functionName", functionName },
                { "instanceId", instanceId },
                { "input", input },
                { "clientRef", clientRef },
            };

            await host.CallAsync(startFunction, args);
            TestDurableClient client = clientRef[0];
            output.WriteLine($"Started {functionName}, Instance ID = {client.InstanceId}");
            return client;
        }

        public static async Task<TestEntityClient> GetEntityClientAsync(
            this ITestHost host,
            EntityId entityId,
            ITestOutputHelper output)
        {
            var startFunction = typeof(ClientFunctions)
                .GetMethod(nameof(ClientFunctions.GetEntityClient));

            var clientRef = new TestEntityClient[1];
            var args = new Dictionary<string, object>
            {
                { "entityId", entityId },
                { "clientRef", clientRef },
            };

            await host.CallAsync(startFunction, args);
            TestEntityClient client = clientRef[0];
            return client;
        }

        /// <summary>
        /// Helper function for the IDurableEntityClientBinding test. Gets an IDurableEntityClient.
        /// </summary>
        public static async Task<IDurableEntityClient> GetEntityClientBindingTest(
            this ITestHost host,
            ITestOutputHelper output)
        {
            var startFunction = typeof(ClientFunctions)
                .GetMethod(nameof(ClientFunctions.GetEntityClientBindingTest));

            var clientRef = new IDurableEntityClient[1];
            var args = new Dictionary<string, object>
            {
                { "clientRef", clientRef },
            };

            await host.CallAsync(startFunction, args);
            IDurableEntityClient client = clientRef[0];
            return client;
        }

        /// <summary>
        /// Helper function for the IDurableOrchestrationClientBinding test. Gets an IDurableOrchestrationClient.
        /// </summary>
        public static async Task<IDurableOrchestrationClient> GetOrchestrationClientBindingTest(
            this ITestHost host,
            ITestOutputHelper output)
        {
            var startFunction = typeof(ClientFunctions)
                .GetMethod(nameof(ClientFunctions.GetOrchestrationClientBindingTest));

            var clientRef = new IDurableOrchestrationClient[1];
            var args = new Dictionary<string, object>
            {
                { "clientRef", clientRef },
            };

            await host.CallAsync(startFunction, args);
            IDurableOrchestrationClient client = clientRef[0];
            return client;
        }
    }
}
