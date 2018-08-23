// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// These helpers are specific to Functions v2.
    /// IMPORTANT: Method signatures must be kept source compatible with the Functions v1 version.
    /// </summary>
    public static class PlatformSpecificHelpers
    {
        public static JobHost CreateJobHost(
            IOptions<DurableTaskOptions> options,
            ILoggerProvider loggerProvider,
            INameResolver nameResolver)
        {
            IHost host = new HostBuilder()
                .ConfigureLogging(
                    loggingBuilder =>
                    {
                        loggingBuilder.AddProvider(loggerProvider);
                    })
                .ConfigureWebJobs(
                    webJobsBuilder =>
                    {
                        webJobsBuilder.AddDurableTask(options);
                        webJobsBuilder.AddAzureStorage();
                    })
                .ConfigureServices(
                    serviceCollection =>
                    {
                        ITypeLocator typeLocator = TestHelpers.GetTypeLocator();
                        serviceCollection.AddSingleton(typeLocator);
                        serviceCollection.AddSingleton(nameResolver);
                    })
                .Build();

            return (JobHost)host.Services.GetService<IJobHost>();
        }
    }
}
