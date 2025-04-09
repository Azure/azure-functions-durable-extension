// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
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

        public BindingHelper(DurableTaskExtension config)
        {
            this.config = config;
        }

        public IAsyncCollector<StartOrchestrationArgs> CreateAsyncCollector(DurableClientAttribute clientAttribute)
        {
            IDurableOrchestrationClient client = this.config.GetClient(clientAttribute);
            return new OrchestrationClientAsyncCollector(client);
        }

        public string DurableOrchestrationClientToString(IDurableOrchestrationClient client, DurableClientAttribute attr)
        {
            if (this.config.OutOfProcProtocol == OutOfProcOrchestrationProtocol.MiddlewarePassthrough)
            {
                // Out-of-proc v2 (aka middleware passthrough) uses gRPC instead of vanilla HTTP + JSON as the RPC protocol.
                string? localRpcAddress = this.config.GetLocalRpcAddress();
                if (localRpcAddress == null)
                {
                    throw new InvalidOperationException("The local RPC address has not been configured!");
                }

                return JsonConvert.SerializeObject(new OrchestrationClientInputData
                {
                    TaskHubName = string.IsNullOrEmpty(attr.TaskHub) ? client.TaskHubName : attr.TaskHub,
                    ConnectionName = attr.ConnectionName,
                    RpcBaseUrl = localRpcAddress,
                    RequiredQueryStringParameters = this.config.HttpApiHandler.GetUniversalQueryStrings(),
                    HttpBaseUrl = this.config.HttpApiHandler.GetBaseUrl(),
                    MaxGrpcMessageSizeInBytes = this.config.Options.MaxGrpcMessageSizeInBytes,
                });
            }

            var payload = new OrchestrationClientInputData
            {
                TaskHubName = client.TaskHubName,
                CreationUrls = this.config.HttpApiHandler.GetInstanceCreationLinks(),
                ManagementUrls = this.config.HttpApiHandler.CreateHttpManagementPayload(InstanceIdPlaceholder, attr?.TaskHub, attr?.ConnectionName),
                BaseUrl = this.config.HttpApiHandler.GetBaseUrl(),
                RequiredQueryStringParameters = this.config.HttpApiHandler.GetUniversalQueryStrings(),
            };

            if (this.config.HttpApiHandler.TryGetRpcBaseUrl(out Uri rpcBaseUrl))
            {
                // If an RPC URL is not available, the out-of-proc durable client SDK is expected to fail.
                // In the case of JavaScript, however, the client SDK is expected to revert to legacy behavior.
                payload.RpcBaseUrl = rpcBaseUrl.OriginalString;
            }

            return JsonConvert.SerializeObject(payload);
        }

        public StartOrchestrationArgs? JObjectToStartOrchestrationArgs(JObject input, DurableClientAttribute attr)
        {
            return input?.ToObject<StartOrchestrationArgs>();
        }

        public StartOrchestrationArgs? StringToStartOrchestrationArgs(string input, DurableClientAttribute attr)
        {
            return !string.IsNullOrEmpty(input) ? JsonConvert.DeserializeObject<StartOrchestrationArgs>(input) : null;
        }

        private class OrchestrationClientAsyncCollector : IAsyncCollector<StartOrchestrationArgs>
        {
            private readonly IDurableOrchestrationClient client;

            public OrchestrationClientAsyncCollector(IDurableOrchestrationClient client)
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
            public string? TaskHubName { get; set; }

            [JsonProperty("connectionName")]
            public string? ConnectionName { get; set; }

            [JsonProperty("creationUrls")]
            public HttpCreationPayload? CreationUrls { get; set; }

            [JsonProperty("managementUrls")]
            public HttpManagementPayload? ManagementUrls { get; set; }

            [JsonProperty("baseUrl")]
            public string? BaseUrl { get; set; }

            [JsonProperty("requiredQueryStringParameters")]
            public string? RequiredQueryStringParameters { get; set; }

            /// <summary>
            /// The URL used by the client binding object to use when calling back into
            /// the extension. For the original out-of-proc implementation, this is a simple
            /// HTTP endpoint. For out-of-proc "v2" (middelware passthrough), this is a gRPC endpoint.
            /// </summary>
            [JsonProperty("rpcBaseUrl")]
            public string? RpcBaseUrl { get; set; }

            /// <summary>
            /// The base URL of the Azure Functions host, used in the out-of-proc model.
            /// This URL is sent by the client binding object to the Durable Worker extension,
            /// allowing the extension to know the host's base URL for constructing management URLs.
            /// </summary>
            [JsonProperty("httpBaseUrl")]
            public string? HttpBaseUrl { get; set; }

            /// <summary>
            /// Optional setting that specifies the maximum gRPC receive message size (in bytes) for the DurableTaskClient.
            /// Defaults to 4,194,304 bytes (4 MB).
            /// </summary>
            [JsonProperty("maxGrpcMessageSizeInBytes")]
            public int? MaxGrpcMessageSizeInBytes { get; set; }
        }
    }
}
