// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Collection("Non-Parallel Collection")]
    public class DurableClientFactoryTests
    {
        public enum RegistrationOrder
        {
            HostOnly,
            ClientFactoryFirst,
            ClientFactoryLast,
        }

        [Theory]
        [InlineData(RegistrationOrder.HostOnly, false, false)]
        [InlineData(RegistrationOrder.HostOnly, false, true)]
        [InlineData(RegistrationOrder.HostOnly, true, false)]
        [InlineData(RegistrationOrder.HostOnly, true, true)]
        [InlineData(RegistrationOrder.ClientFactoryFirst, false, false)]
        [InlineData(RegistrationOrder.ClientFactoryFirst, false, true)]
        [InlineData(RegistrationOrder.ClientFactoryFirst, true, false)]
        [InlineData(RegistrationOrder.ClientFactoryFirst, true, true)]
        [InlineData(RegistrationOrder.ClientFactoryLast, false, false)]
        [InlineData(RegistrationOrder.ClientFactoryLast, false, true)]
        [InlineData(RegistrationOrder.ClientFactoryLast, true, false)]
        [InlineData(RegistrationOrder.ClientFactoryLast, true, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostFactory_DefaultHub_RejectsBeforeCreatingClient(
            RegistrationOrder order, bool external, bool useDefaultOptions)
        {
            return InSlot(async () =>
            {
                DurableTaskOptions options = CreateOptions();
                Mock<IDurabilityProviderFactory> provider = CreateProvider();
                using IHost host = CreateHost(options, provider, order, services =>
                    services.Configure<DurableClientOptions>(clientOptions =>
                    {
                        clientOptions.TaskHub = options.HubName;
                        clientOptions.IsExternalClient = external;
                    }));
                await host.StartAsync();
                try
                {
                    var jobHost = (JobHost)host.Services.GetRequiredService<IJobHost>();
                    Exception exception = await Record.ExceptionAsync(() => jobHost.CallAsync(
                        typeof(LocalClientFunction).GetMethod(nameof(LocalClientFunction.Acquire)),
                        new Dictionary<string, object>
                        {
                            ["external"] = external,
                            ["useDefaultOptions"] = useDefaultOptions,
                        }));

                    AssertSlotError(exception);
                    provider.Verify(p => p.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()), Times.Never);
                }
                finally
                {
                    await host.StopAsync();
                }
            });
        }

        [Theory]
        [InlineData(RegistrationOrder.HostOnly)]
        [InlineData(RegistrationOrder.ClientFactoryFirst)]
        [InlineData(RegistrationOrder.ClientFactoryLast)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostFactory_ResolutionDoesNotInitializeOrValidateUnusedExtension(RegistrationOrder order)
        {
            return InSlot(async () =>
            {
                Mock<IDurabilityProviderFactory> provider = CreateProvider();
                using IHost host = CreateHost(CreateOptions(), provider, order);
                IDurableClientFactory factory = host.Services.GetRequiredService<IDurableClientFactory>();
                Assert.NotNull(factory);
                provider.Verify(p => p.GetDurabilityProvider(), Times.Never);
                provider.Verify(p => p.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()), Times.Never);

                await host.StartAsync();
                Assert.Same(factory, host.Services.GetRequiredService<IDurableClientFactory>());
                await host.StopAsync();
                provider.Verify(p => p.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()), Times.Never);
            });
        }

        [Theory]
        [InlineData(RegistrationOrder.HostOnly)]
        [InlineData(RegistrationOrder.ClientFactoryFirst)]
        [InlineData(RegistrationOrder.ClientFactoryLast)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostFactory_ConfiguredHub_PreservesExternalTargetAndCache(RegistrationOrder order)
        {
            return InSlot(async () =>
            {
                Mock<IDurabilityProviderFactory> provider = CreateProvider();
                using IHost host = CreateHost(CreateOptions("ConfiguredHostHub"), provider, order);
                await host.StartAsync();
                try
                {
                    IDurableClientFactory factory = host.Services.GetRequiredService<IDurableClientFactory>();
                    var clientOptions = new DurableClientOptions { TaskHub = "RemoteHub", ConnectionName = "RemoteStorage" };
                    IDurableClient client = factory.CreateClient(clientOptions);
                    Assert.Equal("RemoteHub", client.TaskHubName);
                    Assert.Same(client, factory.CreateClient(clientOptions));
                    provider.Verify(
                        p => p.GetDurabilityProvider(It.Is<DurableClientAttribute>(
                            a => a.TaskHub == "RemoteHub" && a.ConnectionName == "RemoteStorage" && a.ExternalClient)),
                        Times.Once);
                }
                finally
                {
                    await host.StopAsync();
                }
            });
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task StandaloneFactory_SharedHostOptions_DoesNotInheritHostValidation()
        {
            return InSlot(async () =>
            {
                DurableTaskOptions options = CreateOptions();
                Mock<IDurabilityProviderFactory> provider = CreateProvider();
                using (IHost host = CreateHost(options, provider))
                {
                    await host.StartAsync();
                    host.Services.GetRequiredService<IDurableClientFactory>();
                    await host.StopAsync();
                }

                var services = new ServiceCollection();
                services.AddLogging();
                services.AddOptions();
                services.AddDurableClientFactory(clientOptions =>
                {
                    clientOptions.TaskHub = "RemoteHub";
                    clientOptions.ConnectionName = "RemoteStorage";
                });
                services.AddSingleton<IOptions<DurableTaskOptions>>(new OptionsWrapper<DurableTaskOptions>(options));
                services.RemoveAll<IDurabilityProviderFactory>();
                services.AddSingleton(provider.Object);
                using ServiceProvider standalone = services.BuildServiceProvider();
                IDurableClientFactory factory = standalone.GetRequiredService<IDurableClientFactory>();
                IDurableClient client = factory.CreateClient();
                Assert.True(options.IsDefaultHubName());
                Assert.Equal("RemoteHub", client.TaskHubName);
                Assert.Same(client, factory.CreateClient());
                provider.Verify(
                    p => p.GetDurabilityProvider(It.Is<DurableClientAttribute>(
                        a => a.TaskHub == "RemoteHub" && a.ConnectionName == "RemoteStorage" && a.ExternalClient)),
                    Times.Once);
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostRegistration_PreservesCustomClientFactory(bool registerFirst)
        {
            return InSlot(() =>
            {
                IDurableClientFactory custom = new Mock<IDurableClientFactory>(MockBehavior.Strict).Object;
                using IHost host = new HostBuilder()
                    .ConfigureWebJobs(builder =>
                    {
                        if (registerFirst)
                        {
                            builder.Services.AddSingleton(custom);
                        }

                        builder.Services.AddDurableClientFactory();
                        builder.AddDurableTask();
                        builder.Services.AddDurableClientFactory();
                        if (!registerFirst)
                        {
                            builder.Services.AddSingleton(custom);
                        }
                    })
                    .Build();
                Assert.Same(custom, host.Services.GetRequiredService<IDurableClientFactory>());
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostFactory_InvalidClientOptions_KeepArgumentValidation(string taskHub)
        {
            return InSlot(() =>
            {
                Mock<IDurabilityProviderFactory> provider = CreateProvider();
                using IHost host = CreateHost(CreateOptions(), provider);
                IDurableClientFactory factory = host.Services.GetRequiredService<IDurableClientFactory>();
                Assert.Contains("Please configure 'DurableClientOptions'", Assert.Throws<ArgumentException>(
                    () => factory.CreateClient(null)).Message);
                Assert.Contains("Please provide value for 'TaskHub'", Assert.Throws<ArgumentException>(
                    () => factory.CreateClient(new DurableClientOptions { TaskHub = taskHub })).Message);
                Assert.Contains("Please provide value for 'TaskHub'", Assert.Throws<ArgumentException>(
                    () => factory.CreateClient()).Message);
                provider.Verify(p => p.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()), Times.Never);
                return Task.CompletedTask;
            });
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostFactory_ValidatesBeforeReturningCachedClient()
        {
            return InSlot(async () =>
            {
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "Production");
                DurableTaskOptions options = CreateOptions();
                Mock<IDurabilityProviderFactory> provider = CreateProvider();
                using IHost host = CreateHost(options, provider);
                await host.StartAsync();
                try
                {
                    IDurableClientFactory factory = host.Services.GetRequiredService<IDurableClientFactory>();
                    var clientOptions = new DurableClientOptions { TaskHub = options.HubName };
                    Assert.NotNull(factory.CreateClient(clientOptions));
                    Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "staging");
                    AssertSlotError(Record.Exception(() => factory.CreateClient(clientOptions)));
                    provider.Verify(p => p.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()), Times.Once);
                }
                finally
                {
                    await host.StopAsync();
                }
            });
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostFactory_UsesCanonicalOptionsResolvedAfterFactoryCreation()
        {
            return InSlot(async () =>
            {
                DurableTaskOptions options = CreateOptions("%HostHub%");
                Mock<IDurabilityProviderFactory> provider = CreateProvider();
                var resolver = new Mock<INameResolver>();
                resolver.Setup(r => r.Resolve("HostHub")).Returns("ResolvedHostHub");
                using IHost host = CreateHost(options, provider, configure: s => s.AddSingleton(resolver.Object));
                IDurableClientFactory factory = host.Services.GetRequiredService<IDurableClientFactory>();
                await host.StartAsync();
                try
                {
                    Assert.Equal("ResolvedHostHub", options.HubName);
                    var clientOptions = new DurableClientOptions { TaskHub = "RemoteHub" };
                    Assert.NotNull(factory.CreateClient(clientOptions));
                    options.SetDefaultHubName("SanitizedDefaultHub");
                    AssertSlotError(Record.Exception(() => factory.CreateClient(clientOptions)));
                    provider.Verify(p => p.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()), Times.Once);
                }
                finally
                {
                    await host.StopAsync();
                }
            });
        }

        private static DurableTaskOptions CreateOptions(string hubName = null)
        {
            var options = new DurableTaskOptions
            {
                LocalRpcEndpointEnabled = false,
                WebhookUriProviderOverride = () => new Uri("https://unit.test.invalid"),
            };
            if (hubName != null)
            {
                options.HubName = hubName;
            }

            return options;
        }

        private static Mock<IDurabilityProviderFactory> CreateProvider()
        {
            var backend = new DurabilityProvider(
                "InMemoryOnly",
                new Mock<IOrchestrationService>(MockBehavior.Strict).Object,
                new Mock<IOrchestrationServiceClient>(MockBehavior.Strict).Object,
                "UnusedConnection");
            var provider = new Mock<IDurabilityProviderFactory>(MockBehavior.Strict);
            provider.SetupGet(p => p.Name).Returns("AzureStorage");
            provider.Setup(p => p.GetDurabilityProvider()).Returns(backend);
            provider.Setup(p => p.GetDurabilityProvider(It.IsAny<DurableClientAttribute>())).Returns(backend);
            provider.Setup(p => p.SetUseSeparateQueueForEntityWorkItems(It.IsAny<bool>()));
            return provider;
        }

        private static IHost CreateHost(
            DurableTaskOptions options,
            Mock<IDurabilityProviderFactory> provider,
            RegistrationOrder order = RegistrationOrder.HostOnly,
            Action<IServiceCollection> configure = null)
        {
            return new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    if (order == RegistrationOrder.ClientFactoryFirst)
                    {
                        builder.Services.AddDurableClientFactory();
                    }

                    builder.AddDurableTask(new OptionsWrapper<DurableTaskOptions>(options));
                    if (order == RegistrationOrder.ClientFactoryLast)
                    {
                        builder.Services.AddDurableClientFactory();
                    }
                })
                .ConfigureServices(services =>
                {
                    services.RemoveAll<IDurabilityProviderFactory>();
                    services.AddSingleton(provider.Object);
                    services.RemoveAll<ITelemetryActivator>();
                    services.AddSingleton<ITypeLocator>(new FunctionTypeLocator());
                    services.AddSingleton<IApplicationLifetimeWrapper>(new TestHostShutdownNotificationService());
                    services.AddSingleton(TestHelpers.GetMockPlatformInformationService());
                    configure?.Invoke(services);
                })
                .Build();
        }

        private static void AssertSlotError(Exception exception)
        {
            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception.GetBaseException());
            Assert.Contains("Task Hub name must be specified", exception.ToString());
        }

        private static async Task InSlot(Func<Task> action)
        {
            string site = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string slot = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");
            string runtime = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "FactoryTestHub");
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "staging");
                Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet");
                await action();
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", site);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", slot);
                Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", runtime);
            }
        }

        public sealed class LocalClientFunction
        {
            private readonly IDurableClientFactory factory;
            private readonly IOptions<DurableTaskOptions> options;

            public LocalClientFunction(IDurableClientFactory factory, IOptions<DurableTaskOptions> options)
            {
                this.factory = factory;
                this.options = options;
            }

            [NoAutomaticTrigger]
            public void Acquire(bool external, bool useDefaultOptions)
            {
                if (useDefaultOptions)
                {
                    this.factory.CreateClient();
                }
                else
                {
                    this.factory.CreateClient(new DurableClientOptions
                    {
                        TaskHub = this.options.Value.HubName,
                        IsExternalClient = external,
                    });
                }
            }
        }

        private sealed class FunctionTypeLocator : ITypeLocator
        {
            public IReadOnlyList<Type> GetTypes() => new[] { typeof(LocalClientFunction) };
        }
    }
}
