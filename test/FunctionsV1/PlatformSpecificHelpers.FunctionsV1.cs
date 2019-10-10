// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Azure;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// These helpers are specific to Functions v1.
    /// IMPORTANT: Method signatures must be kept source compatible with the Functions v2 version.
    /// </summary>
    public static class PlatformSpecificHelpers
    {
        public const string VersionSuffix = "V1";
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
            var config = new JobHostConfiguration { HostId = "durable-task-host" };
            config.TypeLocator = TestHelpers.GetTypeLocator();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);

            RegisterConfigBasedOnStorageProvider(config, options, storageProvider, loggerFactory, nameResolver, durableHttpMessageHandler, lifeCycleNotificationHelper);

            // Mock INameResolver for not setting EnvironmentVariables.
            if (nameResolver != null)
            {
                config.AddService(nameResolver);
            }

            // Performance is *significantly* worse when dashboard logging is enabled, at least
            // when running in the storage emulator. Disabling to keep tests running quickly.
            config.DashboardConnectionString = null;

            // Add test logger
            config.LoggerFactory = loggerFactory;

            var host = new JobHost(config);
            return host;
        }

        private static void RegisterConfigBasedOnStorageProvider(
            JobHostConfiguration config,
            DurableTaskOptions options,
            string storageProvider,
            LoggerFactory loggerFactory,
            INameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper)
        {
            switch (storageProvider)
            {
                case TestHelpers.AzureStorageProviderType:
                    {
                        var wrappedOptions = new OptionsWrapper<DurableTaskAzureStorageOptions>(options as DurableTaskAzureStorageOptions);
                        var connectionResolver = new WebJobsConnectionStringProvider();
                        var orchestrationServiceFactory = new AzureStorageOrchestrationServiceFactory(wrappedOptions, connectionResolver);
                        var extension = new DurableTaskExtensionAzureStorageConfig(wrappedOptions, loggerFactory, nameResolver, orchestrationServiceFactory, durableHttpMessageHandler, lifeCycleNotificationHelper);
                        config.Use(extension);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"The DurableTaskOptions of type {options.GetType()} is not supported for tests in Functions V1.");
            }
        }
    }
}