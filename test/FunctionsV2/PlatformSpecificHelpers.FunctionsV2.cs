// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
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
        public const string TestCategory = "Functions" + VersionSuffix;
        public const string FlakeyTestCategory = TestCategory + "_Flakey";

        public static JobHost CreateJobHost(
            DurableTaskOptions options,
            ILoggerProvider loggerProvider,
            INameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper)
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
                        webJobsBuilder.AddCorrectDurableTaskExtension(options);
                        webJobsBuilder.AddAzureStorage();
                    })
                .ConfigureServices(
                    serviceCollection =>
                    {
                        ITypeLocator typeLocator = TestHelpers.GetTypeLocator();
                        serviceCollection.AddSingleton(typeLocator);
                        serviceCollection.AddSingleton(nameResolver);
                        serviceCollection.AddSingleton(durableHttpMessageHandler);

                        if (lifeCycleNotificationHelper != null)
                        {
                            serviceCollection.AddSingleton(lifeCycleNotificationHelper);
                        }
                    })
                .Build();

            return (JobHost)host.Services.GetService<IJobHost>();
        }

        private static IWebJobsBuilder AddCorrectDurableTaskExtension(this IWebJobsBuilder builder, DurableTaskOptions options)
        {
            switch (options)
            {
                case DurableTaskAzureStorageOptions azureOptions:
                    builder.AddAzureStorageDurableTask(new OptionsWrapper<DurableTaskAzureStorageOptions>(azureOptions));
                    return builder;
                case DurableTaskRedisOptions redisOptions:
                    builder.AddRedisDurableTask(new OptionsWrapper<DurableTaskRedisOptions>(redisOptions));
                    return builder;
                case DurableTaskEmulatorOptions emulatorOptions:
                    builder.AddEmulatorDurableTask(new OptionsWrapper<DurableTaskEmulatorOptions>(emulatorOptions));
                    return builder;
                default:
                    throw new InvalidOperationException($"The DurableTaskOptions of type {options.GetType()} is not supported for tests in Functions V2.");
            }
        }
    }
}
