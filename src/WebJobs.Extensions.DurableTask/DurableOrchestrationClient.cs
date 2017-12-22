// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Client for starting, querying, terminating, and raising events to new or existing orchestration instances.
    /// </summary>
    public class DurableOrchestrationClient : DurableOrchestrationClientBase
    {
        internal DurableOrchestrationClient(
            IOrchestrationServiceClient serviceClient,
            DurableTaskExtension config,
            OrchestrationClientAttribute attribute,
            EndToEndTraceHelper traceHelper) : base(
            serviceClient,
            config,
            attribute,
            traceHelper)
        {
        }
    }
}
