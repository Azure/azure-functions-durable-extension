// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private static HttpClient httpClient = null;

        public LifeCycleNotificationHelper(DurableTaskExtension config, ExtensionConfigContext extensionConfigContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.extensionConfigContext = extensionConfigContext ?? throw new ArgumentNullException(nameof(extensionConfigContext));

            INameResolver nameResolver = extensionConfigContext.Config.GetService<INameResolver>();
            this.eventGridKeyValue = nameResolver.Resolve(config.EventGridKeySettingName);

            if (!string.IsNullOrEmpty(config.EventGridTopicEndpoint))
            {
                if (!string.IsNullOrEmpty(config.EventGridKeySettingName))
                {
                    this.useTrace = true;

                    var retryStatusCode = string.IsNullOrEmpty(config.EventGridPublishRetryHttpStatus) ? Array.Empty<int>() :
                        config.EventGridPublishRetryHttpStatus.Split(',')
                        .Select(x =>
                        {
                            if (int.TryParse(x, out var statusCode))
                            {
                                return statusCode;
                            }

                            return 0;
                        })
                        .Where(x => x > 0).ToArray();

                    // Currently, we support Event Grid Custom Topic for notify the lifecycle event of an orchestrator.
                    // For more detail about the Event Grid, please refer this document.
                    // Post to custom topic for Azure Event Grid
                    // https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                    var handler = new HttpRetryMessageHandler(
                        new HttpClientHandler(),
                        config.EventGridPublishRetryCount,
                        retryAttempt => TimeSpan.FromSeconds(config.EventGridPublishRetryInterval),
                        retryStatusCode);

                    httpClient = new HttpClient(handler);
                    if (!string.IsNullOrEmpty(this.eventGridKeyValue))
                    {
                        httpClient.DefaultRequestHeaders.Add("aeg-sas-key", this.eventGridKeyValue);
                    }
                    else
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

        public void SetHttpMessageHandler(HttpMessageHandler handler)
        {
            httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("aeg-sas-key", this.eventGridKeyValue);
        }

        private async Task SendNotificationAsync(
            EventGridEvent[] eventGridEventArray,
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason,
            FunctionState functionState)
        {
            string json = JsonConvert.SerializeObject(eventGridEventArray);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            Stopwatch stopWatch = Stopwatch.StartNew();

            // Details about the Event Grid REST API
            // https://docs.microsoft.com/en-us/rest/api/eventgrid/
            using (HttpResponseMessage result = await httpClient.PostAsync(this.config.EventGridTopicEndpoint, content))
            {
                var body = await result.Content.ReadAsStringAsync();
                if (result.IsSuccessStatusCode)
                {
                    this.config.TraceHelper.EventGridSuccess(
                        hubName,
                        functionName,
                        functionState,
                        version,
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
                        version,
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
            string version,
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
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, "", FunctionState.Started);
        }

        public async Task OrchestratorCompletedAsync(
            string hubName,
            string functionName,
            string version,
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
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, "", FunctionState.Completed);
        }

        public async Task OrchestratorFailedAsync(
            string hubName,
            string functionName,
            string version,
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
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, reason, FunctionState.Failed);
        }

        public async Task OrchestratorTerminatedAsync(
            string hubName,
            string functionName,
            string version,
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
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, reason, FunctionState.Terminated);
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
                        EventType = orchestrationRuntimeStatus,
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

            [JsonProperty(PropertyName = "eventType")]
            public OrchestrationRuntimeStatus EventType { get; set; }
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
            public object Data { get; set; }

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
            private readonly Func<int, TimeSpan> retryWaitSpanFunc;
            private int[] retryTargetStatus;

            public HttpRetryMessageHandler(HttpMessageHandler messageHandler, int maxRetryCount, Func<int, TimeSpan> retryWaitSpanFunc, int[] retryTargetStatusCode)
                : base(messageHandler)
            {
                this.maxRetryCount = maxRetryCount;
                this.retryWaitSpanFunc = retryWaitSpanFunc;
                this.retryTargetStatus = retryTargetStatusCode;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var tryCount = 0;
                Exception lastException = null;
                do
                {
                    try
                    {
                        var response = await base.SendAsync(request, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            return response;
                        }
                        else if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                        {
                            var statusCode = (int) response.StatusCode;
                            if (this.retryTargetStatus.All(x => x != statusCode))
                            {
                                return response;
                            }
                        }

                        lastException = new LifeCyclePublishingException("EventGrid publish api returned badstatus.", response.StatusCode);
                    }
                    catch (HttpRequestException e)
                    {
                        lastException = e;
                    }

                    tryCount++;

                    await Task.Delay(this.retryWaitSpanFunc(tryCount), cancellationToken);

                } while (this.maxRetryCount >= tryCount);

                throw lastException;
            }
        }

        public class LifeCyclePublishingException : Exception
        {
            public HttpStatusCode StatusCode { get; }

            public LifeCyclePublishingException(string message, HttpStatusCode statusCode)
                : base(message)
            {
                this.StatusCode = statusCode;
            }
        }
    }
}
