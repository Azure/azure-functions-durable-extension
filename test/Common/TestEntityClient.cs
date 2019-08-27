// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestEntityClient
    {
        private readonly EntityId entityId;

        public TestEntityClient(
            IDurableEntityClient innerClient,
            EntityId entityId)
        {
            this.InnerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            this.entityId = entityId;
        }

        public IDurableEntityClient InnerClient { get; }

        public async Task SignalEntity(
            ITestOutputHelper output,
            string operationName,
            object operationContent = null)
        {
            output.WriteLine($"Signaling entity {this.entityId} with operation named {operationName}.");
            await this.InnerClient.SignalEntityAsync(this.entityId, operationName, operationContent);
        }

        public async Task<T> WaitForEntityState<T>(
            ITestOutputHelper output,
            TimeSpan? timeout = null)
        {
            if (timeout == null)
            {
                timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
            }

            Stopwatch sw = Stopwatch.StartNew();

            EntityStateResponse<T> response;
            do
            {
                output.WriteLine($"Waiting for {this.entityId} to have state.");

                response = await this.InnerClient.ReadEntityStateAsync<T>(this.entityId);
                if (response.EntityExists)
                {
                    string serializedState = JsonConvert.SerializeObject(response.EntityState);
                    output.WriteLine($"Found state: {serializedState}");
                    return response.EntityState;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            while (sw.Elapsed < timeout);

            throw new TimeoutException($"Durable entity '{this.entityId}' still doesn't have any state!");
        }
    }
}
