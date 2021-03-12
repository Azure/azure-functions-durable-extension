// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Channel;
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

        public static ITestHost CreateJobHost(
            IOptions<DurableTaskOptions> options,
            string storageProvider,
            ILoggerProvider loggerProvider,
            INameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper,
            IMessageSerializerSettingsFactory serializerSettingsFactory,
            IApplicationLifetimeWrapper shutdownNotificationService = null,
            Action<ITelemetry> onSend = null,
#pragma warning disable CS0612 // Type or member is obsolete
            IPlatformInformationService platformInformationService = null)
#pragma warning restore CS0612 // Type or member is obsolete
        {
            var config = new JobHostConfiguration { HostId = "durable-task-host" };
            config.TypeLocator = TestHelpers.GetTypeLocator();

            var connectionResolver = new WebJobsConnectionStringProvider();

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);

            // Unless otherwise specified, use legacy partition management for tests as it makes the task hubs start up faster.
            // These tests run on a single task hub workers, so they don't test partition management anyways, and that is tested
            // in the DTFx repo.
            if (!options.Value.StorageProvider.ContainsKey(nameof(AzureStorageOptions.UseLegacyPartitionManagement)))
            {
                options.Value.StorageProvider.Add(nameof(AzureStorageOptions.UseLegacyPartitionManagement), true);
            }

            platformInformationService = platformInformationService ?? TestHelpers.GetMockPlatformInformationService();

            IDurabilityProviderFactory orchestrationServiceFactory = new AzureStorageDurabilityProviderFactory(
                options,
                connectionResolver,
                nameResolver,
                loggerFactory,
                platformInformationService);

            var extension = new DurableTaskExtension(
                options,
                loggerFactory,
                nameResolver,
                new[] { orchestrationServiceFactory },
                shutdownNotificationService ?? new TestHostShutdownNotificationService(),
                durableHttpMessageHandler,
                lifeCycleNotificationHelper,
                serializerSettingsFactory,
                platformInformationService);
            config.UseDurableTask(extension);

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
            return new FunctionsV1HostWrapper(host, options, connectionResolver);
        }

        private class FunctionsV1HostWrapper : ITestHost
        {
            private readonly JobHost innerHost;
            private readonly DurableTaskOptions options;
            private readonly IConnectionStringResolver connectionResolver;

            public FunctionsV1HostWrapper(
                JobHost innerHost,
                IOptions<DurableTaskOptions> options,
                IConnectionStringResolver connectionResolver)
            {
                this.innerHost = innerHost ?? throw new ArgumentNullException(nameof(innerHost));
                this.options = options.Value;
                this.connectionResolver = connectionResolver;
            }

            public Task CallAsync(string methodName, IDictionary<string, object> args)
                => this.innerHost.CallAsync(methodName, args);

            public Task CallAsync(MethodInfo method, IDictionary<string, object> args)
                => this.innerHost.CallAsync(method, args);

            public void Dispose()
            {
                this.innerHost.Dispose();
            }

            public Task StartAsync() => this.innerHost.StartAsync();

            public async Task StopAsync()
            {
                try
                {
                    await this.innerHost.StopAsync();
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
            }
        }
    }
}