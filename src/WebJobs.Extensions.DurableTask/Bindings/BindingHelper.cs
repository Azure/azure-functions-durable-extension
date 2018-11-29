// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class BindingHelper
    {
        private const string InstanceIdPlaceholder = "INSTANCEID";

        private readonly DurableTaskExtension config;
        private readonly EndToEndTraceHelper traceHelper;

        public BindingHelper(DurableTaskExtension config, EndToEndTraceHelper traceHelper)
        {
            this.config = config;
            this.traceHelper = traceHelper;
        }

        public IAsyncCollector<StartOrchestrationArgs> CreateAsyncCollector(OrchestrationClientAttribute clientAttribute)
        {
            DurableOrchestrationClientBase client = this.config.GetClient(clientAttribute);
            return new OrchestrationClientAsyncCollector(client);
        }

        public string DurableOrchestrationClientToString(DurableOrchestrationClient client, OrchestrationClientAttribute attr)
        {
            var payload = new OrchestrationClientInputData
            {
                TaskHubName = client.TaskHubName,
                CreationUrls = this.config.HttpApiHandler.GetInstanceCreationLinks(),
                ManagementUrls = this.config.HttpApiHandler.CreateHttpManagementPayload(InstanceIdPlaceholder, attr?.TaskHub, attr?.ConnectionName),
            };
            return JsonConvert.SerializeObject(payload);
        }

        public StartOrchestrationArgs JObjectToStartOrchestrationArgs(JObject input, OrchestrationClientAttribute attr)
        {
            return input?.ToObject<StartOrchestrationArgs>();
        }

        public StartOrchestrationArgs StringToStartOrchestrationArgs(string input, OrchestrationClientAttribute attr)
        {
            return !string.IsNullOrEmpty(input) ? JsonConvert.DeserializeObject<StartOrchestrationArgs>(input) : null;
        }

        private class OrchestrationClientAsyncCollector : IAsyncCollector<StartOrchestrationArgs>
        {
            private readonly DurableOrchestrationClientBase client;

            public OrchestrationClientAsyncCollector(DurableOrchestrationClientBase client)
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

        private class OrchestrationClientInputData
        {
            [JsonProperty("taskHubName")]
            public string TaskHubName { get; set; }

            [JsonProperty("creationUrls")]
            public HttpCreationPayload CreationUrls { get; set; }

            [JsonProperty("managementUrls")]
            public HttpManagementPayload ManagementUrls { get; set; }
        }
    }
}
