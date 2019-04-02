// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ReleaseMessage
    {
        [JsonProperty(PropertyName = "parent")]
        public string ParentInstanceId { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string LockRequestId { get; set; }
    }
}
