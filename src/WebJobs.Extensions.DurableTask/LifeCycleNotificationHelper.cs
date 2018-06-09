// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LifeCycleNotificationHelper
    {
        private readonly DurableTaskExtension config;
        private readonly ExtensionConfigContext extensionConfigContext;
        private readonly bool useTrace;
        private readonly string eventGridKeyValue;
        private readonly string eventGridTopicEndpoint;
        private static HttpClient httpClient = null;
        private static HttpMessageHandler httpMessageHandler = null;

        public string EventGridKeyValue => this.eventGridKeyValue;

        public string EventGridTopicEndpoint => this.eventGridTopicEndpoint;

        public LifeCycleNotificationHelper(DurableTaskExtension config, ExtensionConfigContext extensionConfigContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.extensionConfigContext = extensionConfigContext ?? throw new ArgumentNullException(nameof(extensionConfigContext));

            INameResolver nameResolver = extensionConfigContext.Config.GetService<INameResolver>();
            this.eventGridKeyValue = nameResolver.Resolve(config.EventGridKeySettingName);
            this.eventGridTopicEndpoint = config.EventGridTopicEndpoint;
            if (nameResolver.TryResolveWholeString(config.EventGridTopicEndpoint, out var endpoint))
            {
                this.eventGridTopicEndpoint = endpoint;
            }

            if (!string.IsNullOrEmpty(this.eventGridTopicEndpoint))
            {
                if (!string.IsNullOrEmpty(config.EventGridKeySettingName))
                {
                    this.useTrace = true;

                    var retryStatusCode = config.EventGridPublishRetryHttpStatus?
                                              .Where(x => Enum.IsDefined(typeof(HttpStatusCode), x))
                                              .Select(x => (HttpStatusCode) x)
                                              .ToArray()
                                          ?? Array.Empty<HttpStatusCode>();

                    // Currently, we support Event Grid Custom Topic for notify the lifecycle event of an orchestrator.
                    // For more detail about the Event Grid, please refer this document.
                    // Post to custom topic for Azure Event Grid
                    // https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                    this.HttpMessageHandler = new HttpRetryMessageHandler(
                        new HttpClientHandler(),
                        config.EventGridPublishRetryCount,
                        config.EventGridPublishRetryInterval,
                        retryStatusCode);

                    if (string.IsNullOrEmpty(this.eventGridKeyValue))
                    {
                        throw new ArgumentException($"Failed to start lifecycle notification feature. Please check the configuration values for {config.EventGridKeySettingName} on AppSettings.");
                    }
                }
                else
                {
                    throw new ArgumentException($"Failed to start lifecycle notification feature. Please check the configuration values for {config.EventGridTopicEndpoint} and {config.EventGridKeySettingName}.");
                }
            }
        }

        public HttpMessageHandler HttpMessageHandler
        {
            get => httpMessageHandler;
            set
            {
                httpClient?.Dispose();
                httpMessageHandler = value;
                httpClient = new HttpClient(httpMessageHandler);
                httpClient.DefaultRequestHeaders.Add("aeg-sas-key", this.eventGridKeyValue);
            }
        }

        private async Task SendNotificationAsync(
            EventGridEvent[] eventGridEventArray,
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            FunctionState functionState)
        {
            string json = JsonConvert.SerializeObject(eventGridEventArray);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            Stopwatch stopWatch = Stopwatch.StartNew();

            // Details about the Event Grid REST API
            // https://docs.microsoft.com/en-us/rest/api/eventgrid/
            HttpResponseMessage result = null;
            try
            {
                result = await httpClient.PostAsync(this.eventGridTopicEndpoint, content);
            }
            catch (Exception e)
            {
                this.config.TraceHelper.EventGridException(
                    hubName,
                    functionName,
                    functionState,
                    instanceId,
                    e.StackTrace,
                    e,
                    reason,
                    stopWatch.ElapsedMilliseconds);
                return;
            }

            using (result)
            {
                var body = await result.Content.ReadAsStringAsync();
                if (result.IsSuccessStatusCode)
                {
                    this.config.TraceHelper.EventGridSuccess(
                        hubName,
                        functionName,
                        functionState,
                        instanceId,
                        body,
                        result.StatusCode,
                        reason,
                        stopWatch.ElapsedMilliseconds);
                }
                else
                {
                    this.config.TraceHelper.EventGridFailed(
                        hubName,
                        functionName,
                        functionState,
                        instanceId,
                        body,
                        result.StatusCode,
                        reason,
                        stopWatch.ElapsedMilliseconds);
                }
            }
        }

        public async Task OrchestratorStartingAsync(
            string hubName,
            string functionName,
            string instanceId,
            FunctionType functionType,
            bool isReplay)
        {
            if (!this.useTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                instanceId,
                "",
                OrchestrationRuntimeStatus.Running);
            await this.SendNotificationAsync(sendObject, hubName, functionName, instanceId, "", FunctionState.Started);
        }

        public async Task OrchestratorCompletedAsync(
            string hubName,
            string functionName,
            string instanceId,
            bool continuedAsNew,
            FunctionType functionType,
            bool isReplay)
        {
            if (!this.useTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                instanceId,
                "",
                OrchestrationRuntimeStatus.Completed);
            await this.SendNotificationAsync(sendObject, hubName, functionName, instanceId, "", FunctionState.Completed);
        }

        public async Task OrchestratorFailedAsync(
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            FunctionType functionType,
            bool isReplay)
        {
            if (!this.useTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                instanceId,
                reason,
                OrchestrationRuntimeStatus.Failed);
            await this.SendNotificationAsync(sendObject, hubName, functionName, instanceId, reason, FunctionState.Failed);
        }

        public async Task OrchestratorTerminatedAsync(
            string hubName,
            string functionName,
            string instanceId,
            string reason)
        {
            if (!this.useTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                instanceId,
                reason,
                OrchestrationRuntimeStatus.Terminated);
            await this.SendNotificationAsync(sendObject, hubName, functionName, instanceId, reason, FunctionState.Terminated);
        }

        private EventGridEvent[] CreateEventGridEvent(
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            OrchestrationRuntimeStatus orchestrationRuntimeStatus)
        {
            return new[]
            {
                new EventGridEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    EventType = "orchestratorEvent",
                    Subject = $"durable/orchestrator/{orchestrationRuntimeStatus}",
                    EventTime = DateTime.UtcNow,
                    Data = new EventGridPayload
                    {
                        HubName = hubName,
                        FunctionName = functionName,
                        InstanceId = instanceId,
                        Reason = reason,
                        RuntimeStatus = orchestrationRuntimeStatus.ToString(),
                    },
                    DataVersion = "1.0",
                },
            };
        }

        private class EventGridPayload
        {
            public EventGridPayload() { }

            [JsonProperty(PropertyName = "hubName")]
            public string HubName { get; set; }

            [JsonProperty(PropertyName = "functionName")]
            public string FunctionName { get; set; }

            [JsonProperty(PropertyName = "instanceId")]
            public string InstanceId { get; set; }

            [JsonProperty(PropertyName = "reason")]
            public string Reason { get; set; }

            [JsonProperty(PropertyName = "runtimeStatus")]
            public string RuntimeStatus { get; set; }
        }

        private class EventGridEvent
        {
            public EventGridEvent() { }

            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "topic")]
            public string Topic { get; set; }

            [JsonProperty(PropertyName = "subject")]
            public string Subject { get; set; }

            [JsonProperty(PropertyName = "data")]
            public EventGridPayload Data { get; set; }

            [JsonProperty(PropertyName = "eventType")]
            public string EventType { get; set; }

            [JsonProperty(PropertyName = "eventTime")]
            public DateTime EventTime { get; set; }

            [JsonProperty(PropertyName = "metadataVersion")]
            public string MetadataVersion { get; }

            [JsonProperty(PropertyName = "dataVersion")]
            public string DataVersion { get; set; }
        }

        internal class HttpRetryMessageHandler : DelegatingHandler
        {
            private readonly int maxRetryCount;
            private readonly TimeSpan retryWaitSpan;
            private readonly HttpStatusCode[] retryTargetStatus;

            public int MaxRetryCount => this.maxRetryCount;

            public TimeSpan RetryWaitSpan => this.retryWaitSpan;

            public HttpStatusCode[] RetryTargetStatus => this.retryTargetStatus;

            public HttpRetryMessageHandler(HttpMessageHandler messageHandler, int maxRetryCount, TimeSpan retryWaitSpan, HttpStatusCode[] retryTargetStatusCode)
                : base(messageHandler)
            {
                this.maxRetryCount = maxRetryCount;
                this.retryWaitSpan = retryWaitSpan;
                this.retryTargetStatus = retryTargetStatusCode;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var tryCount = 0;
                Exception lastException = null;
                HttpResponseMessage response = null;
                do
                {
                    try
                    {
                        response = await base.SendAsync(request, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            return response;
                        }
                        else if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                        {
                            if (this.retryTargetStatus.All(x => x != response.StatusCode))
                            {
                                return response;
                            }
                        }
                    }
                    catch (HttpRequestException e)
                    {
                        lastException = e;
                    }

                    tryCount++;

                    await Task.Delay(this.retryWaitSpan, cancellationToken);

                } while (this.maxRetryCount >= tryCount);

                if (response != null)
                {
                    return response;
                }
                else
                {
                    ExceptionDispatchInfo.Capture(lastException).Throw();
                    return null;
                }
            }
        }
    }
}
