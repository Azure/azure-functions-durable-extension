// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class DurableTaskHostExtensions
    {
        public static async Task<TestOrchestratorClient> StartOrchestratorAsync(
            this JobHost host,
            string functionName,
            object input,
            ITestOutputHelper output,
            string instanceId = null,
            bool useTaskHubFromAppSettings = false)
        {
            var startFunction = useTaskHubFromAppSettings ?
                typeof(ClientFunctions).GetMethod(nameof(ClientFunctions.StartFunctionWithTaskHub)) :
                typeof(ClientFunctions).GetMethod(nameof(ClientFunctions.StartFunction));
            var clientRef = new TestOrchestratorClient[1];
            var args = new Dictionary<string, object>
            {
                { "functionName", functionName },
                { "instanceId", instanceId },
                { "input", input },
                { "clientRef", clientRef },
            };

            await host.CallAsync(startFunction, args);
            TestOrchestratorClient client = clientRef[0];
            output.WriteLine($"Started {functionName}, Instance ID = {client.InstanceId}");
            return client;
        }
    }
}
