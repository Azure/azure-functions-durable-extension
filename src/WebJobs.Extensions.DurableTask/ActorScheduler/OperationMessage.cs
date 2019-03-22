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
    internal class OperationMessage
    {
        [JsonProperty(PropertyName = "op")]
        public string Operation { get; set; }

        [JsonProperty(PropertyName = "signal")]
        public bool IsSignal { get; set; }

        [JsonProperty(PropertyName = "arg")]
        public string Content { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        [JsonProperty(PropertyName = "parent")]
        public string ParentInstanceId { get; set; }

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
