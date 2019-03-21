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
        public string Argument { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        [JsonProperty(PropertyName = "parent")]
        public string ParentInstanceId { get; set; }

        public void SetArgument(object obj)
        {
            if (obj is JToken jtoken)
            {
                this.Argument = jtoken.ToString(Formatting.None);
            }
            else
            {
                this.Argument = MessagePayloadDataConverter.Default.Serialize(obj);
            }
        }

        public TArgument GetArgument<TArgument>()
        {
            return JsonConvert.DeserializeObject<TArgument>(this.Argument);
        }

        public object GetArgument(Type argumentType)
        {
            return JsonConvert.DeserializeObject(this.Argument, argumentType);
        }
    }
}
