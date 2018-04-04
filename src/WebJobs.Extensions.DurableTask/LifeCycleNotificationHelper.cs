// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LifeCycleNotificationHelper
    {
        private readonly DurableTaskExtension config;
        private readonly ExtensionConfigContext extensionConfigContext;
        private readonly string eventGridKeyValue;

        private bool UseTrace { get; }

        private static HttpClient httpClient = null;

        public LifeCycleNotificationHelper(DurableTaskExtension config, ExtensionConfigContext extensionConfigContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.extensionConfigContext = extensionConfigContext ?? throw new ArgumentNullException(nameof(extensionConfigContext));

            if (!string.IsNullOrEmpty(config.EventGridTopicEndpoint) && !string.IsNullOrEmpty(config.EventGridKeySettingName))
            {
                UseTrace = true;
                // Currently, we support Event Grid Custom Topic for notify the lifecycle event of an orchestrator.
                // For more detail about the Event Grid, please refer this document.
                // Post to custom topic for Azure Event Grid
                // https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                httpClient = new HttpClient();
                INameResolver nameResolver = extensionConfigContext.Config.GetService<INameResolver>();
                eventGridKeyValue = nameResolver.Resolve(config.EventGridKeySettingName);
                httpClient.DefaultRequestHeaders.Add("aeg-sas-key", eventGridKeyValue);
            }
        }

        public void SetHttpMessageHandler(HttpMessageHandler handler)
        {
            httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("aeg-sas-key", eventGridKeyValue);
        }

        private async Task SendNotificationAsync(
            EventGridEvent[] eventGridEventArray,
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason,
            FunctionType functionType,
            bool isReplay,
            FunctionState functionState)
        {
            var json = JsonConvert.SerializeObject(eventGridEventArray);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Details about the Event Grid REST API
            // https://docs.microsoft.com/en-us/rest/api/eventgrid/
            var result = await httpClient.PostAsync(this.config.EventGridTopicEndpoint, content);

            if (!result.IsSuccessStatusCode)
            {
                var appName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? string.Empty;
                var slotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? string.Empty;
                var extensionVersion = FileVersionInfo.GetVersionInfo(typeof(DurableTaskExtension).Assembly.Location).FileVersion;
                this.config.TraceHelper.SendMessageFailed(
                    hubName,
                    functionName,
                    version,
                    instanceId,
                    result.StatusCode,
                    result.ReasonPhrase,
                    reason);
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
            if (!this.UseTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                "",
                OrchestrationRuntimeStatus.Running);
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, "", FunctionType.Orchestrator, isReplay, FunctionState.Started);
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
            if (!this.UseTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                "",
                OrchestrationRuntimeStatus.Completed);
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, "", FunctionType.Orchestrator, isReplay, FunctionState.Completed);
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
            if (!this.UseTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                reason,
                OrchestrationRuntimeStatus.Failed);
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, reason, FunctionType.Orchestrator, isReplay, FunctionState.Failed);
        }

        public async Task OrchestratorTerminatedAsync(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason)
        {
            if (!this.UseTrace)
            {
                return;
            }

            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                reason,
                OrchestrationRuntimeStatus.Terminated);
            await this.SendNotificationAsync(sendObject, hubName, functionName, version, instanceId, reason, FunctionType.Orchestrator, false, FunctionState.Terminated);
        }

        private EventGridEvent[] CreateEventGridEvent(
            string hubName,
            string functionName,
            string version,
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
                    Data = new
                    {
                        HubName = hubName,
                        FunctionName = functionName,
                        Version = version,
                        InstanceId = instanceId,
                        Reason = reason,
                        EventType = orchestrationRuntimeStatus,
                        EventTime = DateTime.UtcNow,
                    },
                    DataVersion = "1.0",
                },
            };
        }

        private class EventGridEvent
        {
            public EventGridEvent() { }

            public EventGridEvent(string id, string subject, object data, string eventType, DateTime eventTime, string dataVersion, string topic = null, string metadataVersion = null)
            {
            }

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
    }
}
