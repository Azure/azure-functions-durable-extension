// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class BindingHelper 
    {
        private readonly DurableTaskConfiguration config;
        private readonly EndToEndTraceHelper traceHelper;

        public BindingHelper(DurableTaskConfiguration config, EndToEndTraceHelper traceHelper)
        {
            this.config = config;
            this.traceHelper = traceHelper;
        }

        public IAsyncCollector<StartOrchestrationArgs> CreateAsyncCollector(OrchestrationClientAttribute clientAttribute)
        {
            DurableOrchestrationClient client = this.config.GetClient(clientAttribute);
            return new OrchestrationClientAsyncCollector(client);
        }

        public StartOrchestrationArgs JObjectToStartOrchestrationArgs(JObject input, OrchestrationClientAttribute attr)
        {
            return input?.ToObject<StartOrchestrationArgs>();
        }

        private class OrchestrationClientAsyncCollector : IAsyncCollector<StartOrchestrationArgs>
        {
            private readonly DurableOrchestrationClient client;

            public OrchestrationClientAsyncCollector(DurableOrchestrationClient client)
            {
                this.client = client;
            }

            public Task AddAsync(StartOrchestrationArgs args, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                return this.client.StartNewAsync(
                    args.FunctionName,
                    args.InstanceId,
                    args.Input);
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.CompletedTask;
            }
        }
    }
}
