// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestActorClient
    {
        private readonly ActorId actorId;

        public TestActorClient(
            IDurableOrchestrationClient innerClient,
            ActorId actorId)
        {
            this.InnerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            this.actorId = actorId;
        }

        public IDurableOrchestrationClient InnerClient { get; }

        public async Task SignalActor(
            ITestOutputHelper output,
            string operationName,
            object operationContent = null)
        {
            output.WriteLine($"Signaling actor {this.actorId} with operation named {operationName}.");
            await this.InnerClient.SignalActor(this.actorId, operationName, operationContent);
        }

        public async Task<T> WaitForActorState<T>(
            ITestOutputHelper output,
            TimeSpan? timeout = null)
        {
            if (timeout == null)
            {
                timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
            }

            Stopwatch sw = Stopwatch.StartNew();

            ActorStateResponse<T> response;
            do
            {
                output.WriteLine($"Waiting for {this.actorId} to have state.");

                response = await this.InnerClient.ReadActorState<T>(this.actorId);
                if (response.ActorExists)
                {
                    string serializedState = JsonConvert.SerializeObject(response.ActorState);
                    output.WriteLine($"Found state: {serializedState}");
                    return response.ActorState;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            while (sw.Elapsed < timeout);

            throw new TimeoutException($"Durable actor '{this.actorId}' still doesn't have any state!");
        }
    }
}
