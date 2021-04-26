// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using WebJobs.Extensions.DurableTask.CodeGeneration.Example;

[assembly: WebJobsStartup(typeof(Startup))]
namespace WebJobs.Extensions.DurableTask.CodeGeneration.Example
{
    public class Startup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
        }
    }
}
