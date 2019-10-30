// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ReleaseMessage
    {
        [JsonProperty(PropertyName = "parent")]
        public string ParentInstanceId { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string LockRequestId { get; set; }

        public override string ToString()
        {
            return $"[Release lock {this.LockRequestId} by {this.ParentInstanceId}]";
        }
    }
}
