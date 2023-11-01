// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.Exceptions;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RemoteEntityContext
    {
        public RemoteEntityContext(EntityBatchRequest batchRequest)
        {
            this.Request = batchRequest;
        }

        [JsonProperty("request")]
        internal EntityBatchRequest Request { get; private set; }

        [JsonIgnore]
        internal EntityBatchResult? Result { get; set; }

        internal void EnsureSuccess()
        {
            if (this.Result == null)
            {
                throw new InvalidOperationException("Entity batch request has not been processed yet.");
            }

            if (this.Result.FailureDetails is { } f)
            {
                // TODO: use an entity specific exception type.
                throw new OrchestrationFailureException(f.ErrorMessage);
            }

            List<Exception>? errors = null;
            if (this.Result.Results is not null)
            {
                foreach (OperationResult result in this.Result.Results)
                {
                    if (result.FailureDetails is { } failure)
                    {
                        errors ??= new List<Exception>();
                        errors.Add(new OrchestrationFailureException(failure.ErrorMessage));
                    }
                }
            }

            if (errors is not null)
            {
                throw errors.Count == 1 ? errors[0] : new AggregateException(errors);
            }
        }
    }
}
