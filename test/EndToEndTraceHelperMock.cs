// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;

namespace WebJobs.Extensions.DurableTask.Tests
{
    internal class EndToEndTraceHelperMock : EndToEndTraceHelper
    {
        public EndToEndTraceHelperMock(JobHostConfiguration config, TraceWriter traceWriter) : base(config, traceWriter)
        {

        }
    }
}
