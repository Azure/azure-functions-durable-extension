// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
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

                    // Currently, we support Event Grid Custom Topic for notify the lifecycle event of an orchestrator.
                    // For more detail about the Event Grid, please refer this document.
                    // Post to custom topic for Azure Event Grid
                    // https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                    httpClient = new HttpClient();
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
                        RuntimeStatus = $"{orchestrationRuntimeStatus}",
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
    }
}
