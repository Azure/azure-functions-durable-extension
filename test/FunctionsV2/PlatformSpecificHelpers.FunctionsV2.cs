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
    /// </summary>
    public static class PlatformSpecificHelpers
    {
        public const string VersionSuffix = "V2";
        public const string TestCategory = "Functions" + VersionSuffix;
        public const string FlakeyTestCategory = TestCategory + "_Flakey";

        public static ITestHost CreateJobHost(
            IOptions<DurableTaskOptions> options,
            string storageProvider,
            Type durabilityProviderFactoryType,
            ILoggerProvider loggerProvider,
            INameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper,
            IMessageSerializerSettingsFactory serializerSettingsFactory)
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
                        webJobsBuilder.AddDurableTask(options, storageProvider, durabilityProviderFactoryType);
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

                        if (serializerSettingsFactory != null)
                        {
                            serviceCollection.AddSingleton(serializerSettingsFactory);
                        }
                    })
                .Build();

            return new FunctionsV2HostWrapper(host);
        }

        private static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskOptions> options, string storageProvider, Type durabilityProviderFactoryType = null)
        {
            if (durabilityProviderFactoryType != null)
            {
                builder.Services.AddSingleton(typeof(IDurabilityProviderFactory), typeof(AzureStorageShortenedTimerDurabilityProviderFactory));
                builder.AddDurableTask(options);
                return builder;
            }

            switch (storageProvider)
            {
                case TestHelpers.RedisProviderType:
                    builder.AddRedisDurableTask();
                    break;
                case TestHelpers.EmulatorProviderType:
                    builder.AddEmulatorDurableTask();
                    break;
                case TestHelpers.AzureStorageProviderType:
                    // This provider is built into the default AddDurableTask() call below.
                    break;
                default:
                    throw new InvalidOperationException($"The DurableTaskOptions of type {options.GetType()} is not supported for tests in Functions V2.");
            }

            builder.AddDurableTask(options);
            return builder;
        }

        private static IWebJobsBuilder AddRedisDurableTask(this IWebJobsBuilder builder)
        {
            builder.Services.AddSingleton<IDurabilityProviderFactory, RedisDurabilityProviderFactory>();
            return builder;
        }

        private static IWebJobsBuilder AddEmulatorDurableTask(this IWebJobsBuilder builder)
        {
            builder.Services.AddSingleton<IDurabilityProviderFactory, EmulatorDurabilityProviderFactory>();
            return builder;
        }

        private class FunctionsV2HostWrapper : ITestHost
        {
            private readonly IHost innerHost;
            private readonly JobHost innerWebJobsHost;

            public FunctionsV2HostWrapper(IHost innerHost)
            {
                this.innerHost = innerHost ?? throw new ArgumentNullException(nameof(innerHost));
                this.innerWebJobsHost = (JobHost)this.innerHost.Services.GetService<IJobHost>();
            }

            public Task CallAsync(string methodName, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(methodName, args);

            public Task CallAsync(MethodInfo method, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(method, args);

            public void Dispose() => this.innerHost.Dispose();

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
