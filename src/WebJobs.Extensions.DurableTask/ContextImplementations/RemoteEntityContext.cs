// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using DurableTask.Core;
using DurableTask.Core.Command;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.History;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RemoteEntityContext
    {
        public RemoteEntityContext(EntityBatchRequest batchRequest)
        {
            this.Request = batchRequest;
        }

        [JsonProperty("request")]
        public EntityBatchRequest Request { get; private set; }

        [JsonIgnore]
        internal EntityBatchResult? Result { get; set; }
    }
}
