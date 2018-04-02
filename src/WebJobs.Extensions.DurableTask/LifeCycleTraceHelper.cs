using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LifeCycleTraceHelper
    {
        private readonly ILogger logger;

        public LifeCycleTraceHelper(JobHostConfiguration config, ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
