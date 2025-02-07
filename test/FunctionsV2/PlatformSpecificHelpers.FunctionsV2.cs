// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// These helpers are specific to Functions v2.
    /// </summary>
    internal static class PlatformSpecificHelpers
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
            IMessageSerializerSettingsFactory serializerSettingsFactory,
            bool addDurableClientFactory,
            ITypeLocator typeLocator,
            Action<ScaleOptions> configureScaleOptions = null)
        {
            // Unless specified, use table partition management for tests as it makes the task hubs start up faster.
            // These tests run on a single task hub workers, so they don't test partition management anyways, and that is tested
            // in the DTFx repo.
            if (options.Value.StorageProvider.ContainsKey(nameof(AzureStorageOptions.UseLegacyPartitionManagement)))
            {
                options.Value.StorageProvider.Add(nameof(AzureStorageOptions.UseTablePartitionManagement), false);
            }

            var hostBuilder = new HostBuilder()
                .ConfigureLogging(
                    loggingBuilder =>
                    {
                        loggingBuilder.AddProvider(loggerProvider);
                    })
                .ConfigureWebJobs(
                    webJobsBuilder =>
                    {
                        if (addDurableClientFactory)
                        {
                            webJobsBuilder.AddDurableClientFactoryDurableTask(options);
                        }
                        else
                        {
                            webJobsBuilder.AddDurableTask(options, storageProvider, durabilityProviderFactoryType);
                        }

                        webJobsBuilder.AddAzureStorageBlobs();
                    })
                .ConfigureServices(
                    serviceCollection =>
                    {
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
                    });

            // if a configureScaleOptions action is provided, then we're probably trying to test the host's scaling logic
            // we configure WebJobsScale and set the minimum logging level to `Debug`, as scaling logs are usually at the `Debug` level
            if (configureScaleOptions != null)
            {
                hostBuilder.ConfigureWebJobsScale(
                    (context, builder) =>
                    {
                        // ignore
                    },
                    configureScaleOptions)
                    .ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
            }

            var host = hostBuilder.Build();
            return new FunctionsV2HostWrapper(host, options, nameResolver);
        }

        public static IHost CreateJobHostExternalEnvironment(IStorageServiceClientProviderFactory clientProviderFactory)
        {
            IHost host = new HostBuilder()
                .ConfigureServices(
                    serviceCollection =>
                    {
                        serviceCollection.AddSingleton(clientProviderFactory);
                        serviceCollection.AddDurableClientFactory();
                    })
                .Build();

            return host;
        }

        public static ITestHost CreateJobHostWithMultipleDurabilityProviders(
            IOptions<DurableTaskOptions> options,
            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories)
        {
            IHost host = new HostBuilder()
                .ConfigureWebJobs(
                    webJobsBuilder =>
                    {
                        webJobsBuilder.AddMultipleDurabilityProvidersDurableTask(options, durabilityProviderFactories);
                    })
                .Build();

            return new FunctionsV2HostWrapper(host, options);
        }

        private static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskOptions> options, string storageProvider, Type durabilityProviderFactoryType = null)
        {
            if (durabilityProviderFactoryType != null)
            {
                builder.Services.AddSingleton(typeof(IDurabilityProviderFactory), durabilityProviderFactoryType);
                options.Value.StorageProvider.Add("type", durabilityProviderFactoryType.Name);
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

        private static IWebJobsBuilder AddMultipleDurabilityProvidersDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskOptions> options, IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = null)
        {
            for (int i = 0; i < durabilityProviderFactories?.Count(); i++)
            {
                IDurabilityProviderFactory factory = durabilityProviderFactories.ElementAt(i);
                builder.Services.AddSingleton(typeof(IDurabilityProviderFactory), factory);
            }

            builder.Services.AddSingleton(options);

            var serviceCollection = builder.AddExtension<DurableTaskExtension>()
                .BindOptions<DurableTaskOptions>()
                .Services.AddSingleton<IConnectionInfoResolver, WebJobsConnectionInfoProvider>();

            serviceCollection.TryAddSingleton<IApplicationLifetimeWrapper, HostLifecycleService>();
#pragma warning disable CS0612 // Type or member is obsolete
            serviceCollection.TryAddSingleton<IPlatformInformation, DefaultPlatformInformation>();
#pragma warning restore CS0612 // Type or member is obsolete

            return builder;
        }

        /// <summary>
        /// Registers the services needed for DurableClientFactory and calls AddDurableClientFactory()
        /// which adds the Durable Task extension that uses Azure Storage.
        /// </summary>
        private static IWebJobsBuilder AddDurableClientFactoryDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskOptions> options)
        {
            builder.Services.AddDurableClientFactory();

            builder.Services.AddSingleton(options);

            var serviceCollection = builder.AddExtension<DurableTaskExtension>()
                .BindOptions<DurableTaskOptions>()
                .Services.AddSingleton<IConnectionInfoResolver, WebJobsConnectionInfoProvider>();

            serviceCollection.TryAddSingleton<IApplicationLifetimeWrapper, HostLifecycleService>();

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

        internal class FunctionsV2HostWrapper : ITestHost
        {
            private readonly JobHost innerWebJobsHost;
            private readonly DurableTaskOptions options;
            private readonly INameResolver nameResolver;

            public FunctionsV2HostWrapper(
                IHost innerHost,
                IOptions<DurableTaskOptions> options,
                INameResolver nameResolver)
            {
                this.InnerHost = innerHost ?? throw new ArgumentNullException(nameof(innerHost));
                this.innerWebJobsHost = (JobHost)this.InnerHost.Services.GetService<IJobHost>();
                this.options = options.Value;
                this.nameResolver = nameResolver;
            }

            internal FunctionsV2HostWrapper(
                IHost innerHost,
                IOptions<DurableTaskOptions> options)
            {
                this.InnerHost = innerHost;
                this.innerWebJobsHost = (JobHost)this.InnerHost.Services.GetService<IJobHost>();
                this.options = options.Value;
            }

            internal IHost InnerHost { get; private set; }

            public Task CallAsync(string methodName, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(methodName, args);

            public Task CallAsync(MethodInfo method, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(method, args);

            public void Dispose()
            {
                this.InnerHost.Dispose();
            }

            public Task StartAsync() => this.InnerHost.StartAsync();

            public async Task StopAsync()
            {
                try
                {
                    await this.InnerHost.StopAsync();
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
            }
        }
    }
}
