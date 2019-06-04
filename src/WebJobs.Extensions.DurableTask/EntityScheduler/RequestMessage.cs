// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A message that represents an operation request or a lock request.
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

        public void SetInput(object obj)
        {
            if (obj is JToken jtoken)
            {
                this.Input = jtoken.ToString(Formatting.None);
            }
            else
            {
                this.Input = MessagePayloadDataConverter.Default.Serialize(obj);
            }
        }

        public T GetInput<T>()
        {
            return JsonConvert.DeserializeObject<T>(this.Input);
        }

        public object GetInput(Type inputType)
        {
            return JsonConvert.DeserializeObject(this.Input, inputType);
        }

        public override string ToString()
        {
            if (this.IsLockRequest)
            {
                return $"[Request lock {this.Id} by {this.ParentInstanceId}, position {this.Position}]";
            }
            else
            {
                return $"[{(this.IsSignal ? "Signal" : "Call")} '{this.Operation}' operation {this.Id} by {this.ParentInstanceId}]";
            }
        }
    }
}
