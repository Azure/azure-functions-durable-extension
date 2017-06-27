// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public static class DurableTaskHostExtensions
    {
        public static async Task<TestOrchestratorClient> StartFunctionAsync(
            this JobHost host,
            string functionName,
            object input,
            ITestOutputHelper output)
        {
            var startFunction = typeof(ClientFunctions).GetMethod(nameof(ClientFunctions.StartFunction));
            var clientRef = new TestOrchestratorClient[1];
            var args = new Dictionary<string, object>
            {
                { "functionName", functionName },
                { "input", input },
                { "clientRef", clientRef },
            };

            await host.CallAsync(startFunction, args);
            TestOrchestratorClient client = clientRef[0];
            output.WriteLine($"Started {functionName}, Instance ID = {client.InstanceId}");
            return client;
        }

        public static void ConfigureDurableFunctionTypeLocator(
            this JobHostConfiguration config,
            params Type[] functionSourceTypes)
        {
            var types = new List<Type>(functionSourceTypes);
            types.Add(typeof(ClientFunctions));

            config.TypeLocator = new ExplicitTypeLocator(types.ToArray());
        }

        private static class ClientFunctions
        {
            [NoAutomaticTrigger]
            public static async Task StartFunction(
                [OrchestrationClient] DurableOrchestrationClient client,
                string functionName,
                object input,
                TestOrchestratorClient[] clientRef)
            {
                DateTime instanceCreationTime = DateTime.UtcNow;

                string instanceId = await client.StartNewAsync(functionName, input);
                clientRef[0] = new TestOrchestratorClient(
                    client,
                    functionName,
                    instanceId,
                    instanceCreationTime);
            }
        }

        class ExplicitTypeLocator : ITypeLocator
        {
            private readonly IReadOnlyList<Type> types;

            public ExplicitTypeLocator(params Type[] types)
            {
                this.types = types.ToList().AsReadOnly();
            }

            public IReadOnlyList<Type> GetTypes()
            {
                return this.types;
            }
        }
    }
}
