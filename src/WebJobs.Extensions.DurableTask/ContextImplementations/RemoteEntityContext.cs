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
        public RemoteEntityContext(OperationBatchRequest batchRequest)
        {
            this.Request = batchRequest;
        }

        [JsonProperty("request")]
        public OperationBatchRequest Request { get; private set; }

        [JsonIgnore]
        internal OperationBatchResult? Result { get; set; }
    }
}
