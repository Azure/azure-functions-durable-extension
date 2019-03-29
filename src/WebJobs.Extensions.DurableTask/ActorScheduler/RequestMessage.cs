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
    /// A message that represents an operation request or a lock request
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
        /// The content the operation was called with.
        /// </summary>
        [JsonProperty(PropertyName = "arg", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Content { get; set; }

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
        public ActorId[] LockSet { get; set; }

        /// <summary>
        /// For lock requests involving multiple locks, the message number.
        /// </summary>
        [JsonProperty(PropertyName = "pos", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Position { get; set; }

        [JsonIgnore]
        public bool IsLockMessage => LockSet != null;

        public void SetContent(object obj)
        {
            if (obj is JToken jtoken)
            {
                this.Content = jtoken.ToString(Formatting.None);
            }
            else
            {
                this.Content = MessagePayloadDataConverter.Default.Serialize(obj);
            }
        }

        public T GetContent<T>()
        {
            return JsonConvert.DeserializeObject<T>(this.Content);
        }

        public object GetContent(Type contentType)
        {
            return JsonConvert.DeserializeObject(this.Content, contentType);
        }
    }
}
