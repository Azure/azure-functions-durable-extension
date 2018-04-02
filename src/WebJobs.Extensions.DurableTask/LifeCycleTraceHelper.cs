using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LifeCycleTraceHelper
    {
        private readonly ILogger logger;
        private readonly DurableTaskExtension config;

        private bool UseTrace { get; }

        private static HttpClient httpClient = null;

        public LifeCycleTraceHelper(DurableTaskExtension config, ILogger logger)
        {
            this.config = config;

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!string.IsNullOrEmpty(config.EventGridTopicEndpoint) && !string.IsNullOrEmpty(config.EventGridKey))
            {
                UseTrace = true;
                httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("aeg-sas-key", config.EventGridKey);
            }
        }

        private async Task TraceRequestAsync(EventGridEvent[] eventGridEventArray)
        {
            var json = JsonConvert.SerializeObject(eventGridEventArray);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var result = await httpClient.PostAsync(this.config.EventGridTopicEndpoint, content);

            if (!result.IsSuccessStatusCode)
            {
                this.logger.LogError("Error in sending message to the EventGrid. Please check the host.json configuration durableTask.EventGridTopicEndpoint and EventGridKey. LifeCycleTraceHelper.TraceRequestAsync - Status: {result_StatusCode} Reason Phrase: {result_ReasonPhrase}", result.StatusCode, result.ReasonPhrase);
            }
        }

        public async Task OrchestratorStartingAsync(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string input,
            FunctionType functionType,
            bool isReplay)
        {
            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                "",
                OrchestrationRuntimeStatus.Running);
            await this.TraceRequestAsync(sendObject);
        }

        public async Task OrchestratorCompletedAsync(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string output,
            bool continuedAsNew,
            FunctionType functionType,
            bool isReplay)
        {
            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                "",
                OrchestrationRuntimeStatus.Completed);
            await this.TraceRequestAsync(sendObject);
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
            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                reason,
                OrchestrationRuntimeStatus.Failed);
            await this.TraceRequestAsync(sendObject);
        }

        public async Task OrchestratorTerminatedAsync(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason)
        {
            EventGridEvent[] sendObject = this.CreateEventGridEvent(
                hubName,
                functionName,
                version,
                instanceId,
                reason,
                OrchestrationRuntimeStatus.Terminated);
            await this.TraceRequestAsync(sendObject);
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
