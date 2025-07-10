// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.Core.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A message sent to an entity, such as operation, signal, lock, release, or continue messages.
    /// </summary>
    internal class RequestMessage
    {
        /// <summary>
        /// The name of the operation being called (if this is an operation message) or <c>null</c>
        /// (if this is a lock request).
        /// </summary>
        [JsonProperty(PropertyName = "op")]
        public string Operation { get; set; }

        /// <summary>
        /// Whether or not this is a one-way message.
        /// </summary>
        [JsonProperty(PropertyName = "signal", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsSignal { get; set; }

        /// <summary>
        /// The operation input.
        /// </summary>
        [JsonProperty(PropertyName = "input", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Input { get; set; }

        /// <summary>
        /// A unique identifier for this operation.
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public Guid Id { get; set; }

        /// <summary>
        /// The parent instance that called this operation.
        /// </summary>
        [JsonProperty(PropertyName = "parent", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ParentInstanceId { get; set; }

        /// <summary>
        /// The parent instance that called this operation.
        /// </summary>
        [JsonProperty(PropertyName = "parentExecution", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ParentExecutionId { get; set; }

        /// <summary>
        /// Optionally, a scheduled time at which to start the operation.
        /// </summary>
        [JsonProperty(PropertyName = "due", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime? ScheduledTime { get; set; }

        /// <summary>
        /// A timestamp for this request.
        /// Used for duplicate filtering and in-order delivery.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// A timestamp for the predecessor request in the stream, or DateTime.MinValue if none.
        /// Used for duplicate filtering and in-order delivery.
        /// </summary>
        public DateTime Predecessor { get; set; }

        /// <summary>
        /// For lock requests, the set of locks being acquired. Is sorted,
        /// contains at least one element, and has no repetitions.
        /// </summary>
        [JsonProperty(PropertyName = "lockset", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public EntityId[] LockSet { get; set; }

        /// <summary>
        /// For lock requests involving multiple locks, the message number.
        /// </summary>
        [JsonProperty(PropertyName = "pos", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Position { get; set; }

        [JsonIgnore]
        public bool IsLockRequest => this.LockSet != null;

        /// <summary>
        /// Parent trace context of this request message.
        /// </summary>
        [JsonProperty(PropertyName = "parentTraceContext", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DistributedTraceContext ParentTraceContext { get; set; }

        /// <summary>
        /// The time the request was generated.
        /// </summary>
        [JsonProperty(PropertyName = "requestTime", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTimeOffset? RequestTime { get; set; }

        public void SetInput(object obj, MessagePayloadDataConverter dataConverter)
        {
            try
            {
                if (obj is JToken jtoken)
                {
                    this.Input = jtoken.ToString(Formatting.None);
                }
                else
                {
                    this.Input = dataConverter.Serialize(obj);
                }
            }
            catch (Exception e)
            {
                throw new EntitySchedulerException($"Failed to serialize input for operation '{this.Operation}': {e.Message}", e);
            }
        }

        public T GetInput<T>(MessagePayloadDataConverter dataConverter)
        {
            try
            {
                return dataConverter.Deserialize<T>(this.Input);
            }
            catch (Exception e)
            {
                throw new EntitySchedulerException($"Failed to deserialize input for operation '{this.Operation}': {e.Message}", e);
            }
        }

        public object GetInput(Type inputType, MessagePayloadDataConverter dataConverter)
        {
            try
            {
                return dataConverter.Deserialize(this.Input, inputType);
            }
            catch (Exception e)
            {
                throw new EntitySchedulerException($"Failed to deserialize input for operation '{this.Operation}': {e.Message}", e);
            }
        }

        public DateTime GetAdjustedDeliveryTime(DurabilityProvider durabilityProvider)
        {
            if (this.ScheduledTime.HasValue)
            {
                var now = DateTime.UtcNow;
                if ((this.ScheduledTime.Value - now) <= durabilityProvider.MaximumDelayTime)
                {
                    return this.ScheduledTime.Value;
                }
                else
                {
                    return now + durabilityProvider.LongRunningTimerIntervalLength;
                }
            }
            else
            {
                throw new InvalidOperationException("this is not a delayed message");
            }
        }

        public override string ToString()
        {
            if (this.IsLockRequest)
            {
                return $"[Request lock {this.Id} by {this.ParentInstanceId} {this.ParentExecutionId}, position {this.Position}]";
            }
            else
            {
                return $"[{(this.IsSignal ? "Signal" : "Call")} '{this.Operation}' operation {this.Id} by {this.ParentInstanceId} {this.ParentExecutionId}]";
            }
        }
    }
}
