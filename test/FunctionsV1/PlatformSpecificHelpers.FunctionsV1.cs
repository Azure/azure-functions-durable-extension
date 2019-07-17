// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net.Http;
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
            IOptions<DurableTaskOptions> options,
            ILoggerProvider loggerProvider,
            INameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler)
        {
            var config = new JobHostConfiguration { HostId = "durable-task-host" };
            config.TypeLocator = TestHelpers.GetTypeLocator();

            var connectionResolver = new WebJobsConnectionStringProvider();

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);

            IOrchestrationServiceFactory orchestrationServiceFactory = new OrchestrationServiceFactory(options, connectionResolver);

            var extension = new DurableTaskExtension(options, loggerFactory, nameResolver, orchestrationServiceFactory, durableHttpMessageHandler);
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
            return host;
        }
    }
}