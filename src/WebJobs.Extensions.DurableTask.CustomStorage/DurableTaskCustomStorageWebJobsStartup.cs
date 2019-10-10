// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask.CustomStorage;
using Microsoft.Azure.WebJobs.Hosting;
using WebJobs.Extensions.DurableTask.CustomStorage;

[assembly: WebJobsStartup(typeof(DurableTaskCustomStorageWebJobsStartup))]

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.CustomStorage
{
    internal class DurableTaskCustomStorageWebJobsStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddCustomStorageDurableTask();
        }
    }
}