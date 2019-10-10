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
            string storageProvider,
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
                        webJobsBuilder.AddCorrectDurableTaskExtension(options, storageProvider);
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

        private static IWebJobsBuilder AddCorrectDurableTaskExtension(this IWebJobsBuilder builder, DurableTaskOptions options, string storageProvider)
        {
            switch (storageProvider)
            {
                case TestHelpers.AzureStorageProviderType:
                    builder.AddAzureStorageDurableTask(new OptionsWrapper<DurableTaskAzureStorageOptions>(options as DurableTaskAzureStorageOptions));
                    return builder;
                case TestHelpers.RedisProviderType:
                    builder.AddRedisDurableTask(options);
                    return builder;
                case TestHelpers.EmulatorProviderType:
                    builder.AddEmulatorDurableTask(options);
                    return builder;
                default:
                    throw new InvalidOperationException($"The DurableTaskOptions of type {options.GetType()} is not supported for tests in Functions V2.");
            }
        }

        private static IWebJobsBuilder AddRedisDurableTask(this IWebJobsBuilder builder, DurableTaskOptions options)
        {
            return builder;
        }

        private static IWebJobsBuilder AddEmulatorDurableTask(this IWebJobsBuilder builder, DurableTaskOptions options)
        {
            return builder;
        }
    }
}
