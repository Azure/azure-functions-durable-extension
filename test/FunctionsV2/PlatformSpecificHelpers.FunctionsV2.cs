// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// These helpers are specific to Functions v2.
    /// IMPORTANT: Method signatures must be kept source compatible with the Functions v1 version.
    /// </summary>
    public static class PlatformSpecificHelpers
    {
        public const string VersionSuffix = "V2";
        public const string TestCategory = "Functions" + VersionSuffix;
        public const string FlakeyTestCategory = TestCategory + "_Flakey";

        public static ITestHost CreateJobHost(
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

            return new FunctionsV2HostWrapper(host, options, nameResolver);
        }

        private class FunctionsV2HostWrapper : ITestHost
        {
            private readonly IHost innerHost;
            private readonly JobHost innerWebJobsHost;
            private readonly DurableTaskOptions options;
            private readonly INameResolver nameResolver;

            public FunctionsV2HostWrapper(
                IHost innerHost,
                IOptions<DurableTaskOptions> options,
                INameResolver nameResolver)
            {
                this.innerHost = innerHost ?? throw new ArgumentNullException(nameof(innerHost));
                this.innerWebJobsHost = (JobHost)this.innerHost.Services.GetService<IJobHost>();
                this.options = options.Value;
                this.nameResolver = nameResolver;
            }

            public Task CallAsync(string methodName, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(methodName, args);

            public Task CallAsync(MethodInfo method, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(method, args);

            public void Dispose()
            {
                this.innerHost.Dispose();
#if !DEBUG
                string connectionString = this.nameResolver.Resolve(this.options.AzureStorageConnectionStringName ?? "AzureWebJobsStorage");
                int partitionCount = this.options.PartitionCount;
                string taskHub = this.options.HubName.ToLowerInvariant();
                Task.Run(() => TestHelpers.DeleteAllElementsInStorageTaskHubAsync(connectionString, taskHub, partitionCount)).GetAwaiter().GetResult();
#endif
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
