// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETSTANDARD2_0
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(DurableTaskWebJobsStartup))]

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableTaskWebJobsStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddAzureStorageDurableTask();
        }
    }
}
#endif