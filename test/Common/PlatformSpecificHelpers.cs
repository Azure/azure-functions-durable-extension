// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Channel;
#if !FUNCTIONS_V1
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// These helpers are specific to Functions v2.
    /// </summary>
    internal static class PlatformSpecificHelpers
    {
#if !FUNCTIONS_V1
        public static ITestHost CreateJobHost(
            IOptions<DurableTaskOptions> options,
            Action<IWebJobsBuilder> registerDurableProviderFactory,
            ILoggerProvider loggerProvider,
            INameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper,
            IMessageSerializerSettingsFactory serializerSettingsFactory,
            Action<ITelemetry> onSend,
            bool addDurableClientFactory,
            ITypeLocator typeLocator,
            Func<Task> preHostStartOperation = null,
            Func<Task> postHostStopOperation = null)
        {
            // Unless otherwise specified, use legacy partition management for tests as it makes the task hubs start up faster.
            // These tests run on a single task hub workers, so they don't test partition management anyways, and that is tested
            // in the DTFx repo.
            if (!options.Value.StorageProvider.ContainsKey(nameof(AzureStorageOptions.UseLegacyPartitionManagement)))
            {
                options.Value.StorageProvider.Add(nameof(AzureStorageOptions.UseLegacyPartitionManagement), true);
            }

            IHost host = new HostBuilder()
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
                            registerDurableProviderFactory.Invoke(webJobsBuilder);
                        }

                        webJobsBuilder.AddAzureStorage();
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

                        if (onSend != null)
                        {
                            serviceCollection.AddSingleton<ITelemetryActivator>(serviceProvider =>
                            {
                                var durableTaskOptions = serviceProvider.GetService<IOptions<DurableTaskOptions>>();
                                var envResolver = serviceProvider.GetService<INameResolver>();
                                var telemetryActivator = new TelemetryActivator(durableTaskOptions, envResolver)
                                {
                                    OnSend = onSend,
                                };
                                return telemetryActivator;
                            });
                        }
                    })
                .Build();

            return new FunctionsV2HostWrapper(host, options, nameResolver, preHostStartOperation, postHostStopOperation);
        }

        public static IHost CreateJobHostExternalEnvironment(IConnectionStringResolver connectionStringResolver)
        {
            IHost host = new HostBuilder()
                .ConfigureServices(
                    serviceCollection =>
                    {
                        serviceCollection.AddSingleton(connectionStringResolver);
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

        public static IWebJobsBuilder AddTestDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskOptions> options, Type durabilityProviderFactoryType = null)
        {
            if (durabilityProviderFactoryType != null)
            {
                builder.Services.AddSingleton(typeof(IDurabilityProviderFactory), durabilityProviderFactoryType);
                options.Value.StorageProvider.Add("type", durabilityProviderFactoryType.Name);
                builder.AddDurableTask(options);
                return builder;
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
                .Services.AddSingleton<IConnectionStringResolver, WebJobsConnectionStringProvider>();

            serviceCollection.TryAddSingleton<IApplicationLifetimeWrapper, HostLifecycleService>();
#pragma warning disable CS0612 // Type or member is obsolete
            serviceCollection.TryAddSingleton<IPlatformInformationService, DefaultPlatformInformationProvider>();
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
                .Services.AddSingleton<IConnectionStringResolver, WebJobsConnectionStringProvider>();

            serviceCollection.TryAddSingleton<IApplicationLifetimeWrapper, HostLifecycleService>();

            return builder;
        }

        internal class FunctionsV2HostWrapper : ITestHost
        {
            private readonly IHost innerHost;
            private readonly JobHost innerWebJobsHost;
            private readonly Func<Task> preHostStartOp;
            private readonly Func<Task> postHostStopOp;
            private readonly DurableTaskOptions options;
            private readonly INameResolver nameResolver;

            public FunctionsV2HostWrapper(
                IHost innerHost,
                IOptions<DurableTaskOptions> options,
                INameResolver nameResolver,
                Func<Task> preHostStartOp,
                Func<Task> postHostStopOp)
            {
                this.innerHost = innerHost ?? throw new ArgumentNullException(nameof(innerHost));
                this.innerWebJobsHost = (JobHost)this.innerHost.Services.GetService<IJobHost>();
                this.options = options.Value;
                this.preHostStartOp = preHostStartOp ?? (() => Task.CompletedTask);
                this.postHostStopOp = postHostStopOp ?? (() => Task.CompletedTask);
                this.nameResolver = nameResolver;
            }

            internal FunctionsV2HostWrapper(
                IHost innerHost,
                IOptions<DurableTaskOptions> options)
            {
                this.innerHost = innerHost;
                this.innerWebJobsHost = (JobHost)this.innerHost.Services.GetService<IJobHost>();
                this.options = options.Value;
            }

            public Task CallAsync(string methodName, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(methodName, args);

            public Task CallAsync(MethodInfo method, IDictionary<string, object> args)
                => this.innerWebJobsHost.CallAsync(method, args);

            public void Dispose()
            {
                this.innerHost.Dispose();
            }

            public async Task StartAsync()
            {
                await this.preHostStartOp.Invoke();
                await this.innerHost.StartAsync();
            }

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
                finally
                {
                    try
                    {
                        await this.postHostStopOp.Invoke();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
#else
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
                try
                {
                    this.innerHost.Dispose();
                }
                catch
                {
                    // Sometimes the logger shutdown leads to an ungraceful exception
                    // ignore this
                }
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
#endif
}
