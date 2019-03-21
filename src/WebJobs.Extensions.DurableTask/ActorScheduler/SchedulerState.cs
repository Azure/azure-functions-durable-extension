using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Serializing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The persisted state of an actor scheduler, as handed forward between ContinueAsNew instances.
    /// </summary>
    [JsonConverter(typeof(CustomJsonConverter<SchedulerState>))]
    internal class SchedulerState : CustomJsonConverter<SchedulerState>.IConversion
    {
        /// <summary>
        /// Whether this actor exists or not.
        /// </summary>
        public bool ActorExists { get; set; }

        /// <summary>
        /// The serialized actor state. This can be stale while CurrentStateView != null.
        /// </summary>
        public string ActorState { get; set; }

        /// <summary>
        /// The queue of waiting operations, or null if none.
        /// </summary>
        public Queue<OperationMessage> Queue { get; set; }

        [JsonIgnore]
        public bool IsEmpty => !ActorExists && (Queue == null || Queue.Count == 0);

        [JsonIgnore]
        internal IStateView CurrentStateView { get; set; }

        internal void Enqueue(OperationMessage operationMessage)
        {
            if (this.Queue == null)
            {
                this.Queue = new Queue<OperationMessage>();
            }

            this.Queue.Enqueue(operationMessage);
        }

        internal bool TryDequeue(out OperationMessage operationMessage)
        {
            operationMessage = null;

            if (this.Queue == null)
            {
                return false;
            }

            operationMessage = this.Queue.Dequeue();

            if (this.Queue.Count == 0)
            {
                this.Queue = null;
            }

            return true;
        }

        public override string ToString()
        {
            return $"state.length={((this.ActorState != null) ? this.ActorState.Length : 0)} queue.count={((this.Queue != null) ? this.Queue.Count : 0)}";
        }

        public void FromJson(JsonReader reader, JsonSerializer serializer)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    switch (reader.Value as string)
                    {
                        case "state":
                            {
                                this.ActorState = reader.ReadAsString();
                                this.ActorExists = true;
                                break;
                            }

                        case "queue":
                            {
                                this.Queue = new Queue<OperationMessage>();

                                while (reader.Read())
                                {
                                    if (reader.TokenType == JsonToken.StartObject)
                                    {
                                        this.Queue.Enqueue(serializer.Deserialize<OperationMessage>(reader));
                                    }
                                }

                                break;
                            }

                        default:
                            continue;
                    }
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    return;
                }
            }
        }

        public void ToJson(JsonWriter writer, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            if (this.ActorExists)
            {
                writer.WritePropertyName("state");
                writer.WriteValue(this.ActorState);
            }

            if (this.Queue != null && this.Queue.Count > 0)
            {
                writer.WritePropertyName("queue");
                writer.WriteStartArray();
                foreach (var o in this.Queue)
                {
                    serializer.Serialize(writer, o, typeof(OperationMessage));
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
    }
}