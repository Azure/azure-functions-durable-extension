// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// These helpers are specific to Functions v2.
    /// </summary>
    public static class PlatformSpecificHelpers
    {
        public const string VersionSuffix = "V2";
        public const string TestCategory = "DurableFunctions" + VersionSuffix;
        public const string FlakeyTestCategory = TestCategory + "_Flakey";

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
