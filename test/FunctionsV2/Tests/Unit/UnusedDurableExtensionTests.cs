// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Collection("Non-Parallel Collection")]
    public class UnusedDurableExtensionTests
    {
        [Theory]
        [InlineData(null, "dotnet", TestHelpers.AzureStorageProviderType)]
        [InlineData("Production", "dotnet", TestHelpers.AzureStorageProviderType)]
        [InlineData("staging", "dotnet", TestHelpers.AzureStorageProviderType)]
        [InlineData("staging", "dotnet-isolated", TestHelpers.AzureStorageProviderType)]
        [InlineData("staging", "python", TestHelpers.AzureStorageProviderType)]
        [InlineData("staging", "node", TestHelpers.AzureStorageProviderType)]
        [InlineData("staging", "java", TestHelpers.AzureStorageProviderType)]
        [InlineData("staging", "powershell", TestHelpers.AzureStorageProviderType)]
        [InlineData("staging", "dotnet", TestHelpers.EmulatorProviderType)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostWithoutDurableBindings_DefaultHubName_Starts(string slotName, string runtime, string storageProvider)
        {
            return WithEnvironmentAsync(slotName, runtime, async () =>
            {
                using (ITestHost host = TestHelpers.GetJobHostWithOptions(
                    NullLoggerProvider.Instance,
                    CreateOptions(),
                    storageProviderType: storageProvider,
                    types: new[] { typeof(NonDurableFunctions) }))
                {
                    await host.StartAsync();
                    await host.CallAsync(
                        typeof(NonDurableFunctions).GetMethod(nameof(NonDurableFunctions.Run)),
                        new Dictionary<string, object>());
                    await host.StopAsync();
                }
            });
        }

        [Theory]
        [InlineData(typeof(OrchestratorFunctions))]
        [InlineData(typeof(ActivityFunctions))]
        [InlineData(typeof(EntityFunctions))]
        [InlineData(typeof(ClientFunctions))]
        [InlineData(typeof(LegacyClientFunctions))]
        [InlineData(typeof(ClientOutputFunctions))]
        [InlineData(typeof(OutOfProcessClientFunctions))]
        [InlineData(typeof(OverriddenClientFunctions))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task TaskHubName_DefaultNameNonProductionSlot_ThrowsException(Type functionsType)
        {
            return WithEnvironmentAsync("staging", "dotnet", async () =>
            {
                Exception exception = await Record.ExceptionAsync(async () =>
                {
                    using (ITestHost host = TestHelpers.GetJobHostWithOptions(
                        NullLoggerProvider.Instance,
                        CreateOptions(),
                        types: new[] { functionsType }))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

                Assert.NotNull(exception);
                Assert.IsType<InvalidOperationException>(exception.GetBaseException());
                Assert.Contains("Task Hub name must be specified in host.json when using slots", exception.ToString());
            });
        }

        [Theory]
        [InlineData("ExplicitSlotTaskHub")]
        [InlineData("%SlotTaskHub%")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task ClientBinding_ExplicitHubName_StartsAndBinds(string hubName)
        {
            return WithEnvironmentAsync("staging", "dotnet", async () =>
            {
                DurableTaskOptions options = CreateOptions();
                options.HubName = hubName;
                var resolver = new Mock<INameResolver>();
                resolver.Setup(r => r.Resolve("SlotTaskHub")).Returns("ExplicitSlotTaskHub");

                using (ITestHost host = TestHelpers.GetJobHostWithOptions(
                    NullLoggerProvider.Instance,
                    options,
                    nameResolver: resolver.Object,
                    types: new[] { typeof(ClientFunctions) }))
                {
                    await host.StartAsync();
                    await host.CallAsync(
                        typeof(ClientFunctions).GetMethod(nameof(ClientFunctions.Run)),
                        new Dictionary<string, object>());
                    await host.StopAsync();
                }
            });
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task HostWithoutDurableBindings_InvalidConcurrency_StillThrows()
        {
            return WithEnvironmentAsync("staging", "dotnet", async () =>
            {
                DurableTaskOptions options = CreateOptions();
                options.MaxConcurrentActivityFunctions = 0;
                Exception exception = await Record.ExceptionAsync(async () =>
                {
                    using (ITestHost host = TestHelpers.GetJobHostWithOptions(
                        NullLoggerProvider.Instance,
                        options,
                        types: new[] { typeof(NonDurableFunctions) }))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

                Assert.NotNull(exception);
                Assert.Contains("MaxConcurrentActivityFunctions must be a positive integer value.", exception.ToString());
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task DeferredDurableUse_DefaultHubName_RejectsSlot(bool startWorker)
        {
            return WithEnvironmentAsync("staging", "dotnet", async () =>
            {
                using (ITestHost host = TestHelpers.GetJobHostWithOptions(
                    NullLoggerProvider.Instance,
                    CreateOptions(),
                    types: new[] { typeof(NonDurableFunctions) }))
                {
                    await host.StartAsync();
                    var wrapper = (PlatformSpecificHelpers.FunctionsV2HostWrapper)host;
                    DurableTaskExtension extension = wrapper.InnerHost.Services
                        .GetServices<IExtensionConfigProvider>()
                        .OfType<DurableTaskExtension>()
                        .Single();
                    Exception exception = await Record.ExceptionAsync(async () =>
                    {
                        if (startWorker)
                        {
                            await extension.StartTaskHubWorkerIfNotStartedAsync();
                        }
                        else
                        {
                            extension.GetClient(new DurableClientAttribute());
                        }
                    });

                    Assert.IsType<InvalidOperationException>(exception);
                    Assert.Contains("Task Hub name must be specified in host.json when using slots", exception.Message);
                    await host.StopAsync();
                }
            });
        }

        [Theory]
        [InlineData("staging", "DefaultHub", true)]
        [InlineData("staging", "defaulthub", true)]
        [InlineData("staging", "UniqueHub", false)]
        [InlineData("Production", "DefaultHub", false)]
        [InlineData("production", "DefaultHub", false)]
        [InlineData(null, "DefaultHub", false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task SlotValidation_PreservesDefaultHubComparison(string slotName, string hubName, bool throws)
        {
            return WithEnvironmentAsync(slotName, "dotnet", () =>
            {
                var options = new DurableTaskOptions();
                options.SetDefaultHubName("DefaultHub");
                options.HubName = hubName;

                Exception exception = Record.Exception(options.ValidateHubNameForSlot);
                if (throws)
                {
                    Assert.IsType<InvalidOperationException>(exception);
                }
                else
                {
                    Assert.Null(exception);
                }

                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("ExplicitSlotTaskHub", false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task DeleteTaskHub_ValidatesSlotBeforeCallingBackend(string hubName, bool throws)
        {
            return WithEnvironmentAsync("staging", "dotnet", async () =>
            {
                var service = new Mock<IOrchestrationService>();
                service.Setup(s => s.DeleteAsync()).Returns(Task.CompletedTask);
                var provider = new DurabilityProvider(
                    "TestProvider",
                    service.Object,
                    Mock.Of<IOrchestrationServiceClient>(),
                    "Storage");
                var factory = new Mock<IDurabilityProviderFactory>();
                factory.SetupGet(f => f.Name).Returns("AzureStorage");
                factory.Setup(f => f.GetDurabilityProvider()).Returns(provider);
                DurableTaskOptions options = CreateOptions();
                if (hubName != null)
                {
                    options.HubName = hubName;
                }

                using (var extension = new DurableTaskExtension(
                    new OptionsWrapper<DurableTaskOptions>(options),
                    NullLoggerFactory.Instance,
                    TestHelpers.GetTestNameResolver(),
                    new[] { factory.Object },
                    new TestHostShutdownNotificationService(),
                    platformInformationService: TestHelpers.GetMockPlatformInformationService()))
                {
                    Exception exception = await Record.ExceptionAsync(extension.DeleteTaskHubAsync);
                    if (throws)
                    {
                        Assert.IsType<InvalidOperationException>(exception);
                        Assert.Contains("Task Hub name must be specified in host.json when using slots", exception.Message);
                    }
                    else
                    {
                        Assert.Null(exception);
                    }

                    service.Verify(s => s.DeleteAsync(), throws ? Times.Never() : Times.Once());
                }
            });
        }

        private static DurableTaskOptions CreateOptions()
        {
            return new DurableTaskOptions
            {
                LocalRpcEndpointEnabled = false,
                WebhookUriProviderOverride = () => new Uri("http://localhost"),
            };
        }

        private static async Task WithEnvironmentAsync(string slotName, string runtime, Func<Task> action)
        {
            string originalSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string originalSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");
            string originalRuntime = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "UnusedDurableExtension");
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", slotName);
                Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", runtime);
                await action();
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", originalSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", originalSlotName);
                Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", originalRuntime);
            }
        }

        public static class NonDurableFunctions
        {
            [NoAutomaticTrigger]
            public static void Run()
            {
            }
        }

        public static class OrchestratorFunctions
        {
            public static void Run([OrchestrationTrigger] IDurableOrchestrationContext context)
            {
            }
        }

        public static class ActivityFunctions
        {
            public static void Run([ActivityTrigger] string input)
            {
            }
        }

        public static class EntityFunctions
        {
            public static void Run([EntityTrigger] IDurableEntityContext context)
            {
            }
        }

        public static class ClientFunctions
        {
            [NoAutomaticTrigger]
            public static void Run([DurableClient] IDurableClient client)
            {
                Assert.Equal("ExplicitSlotTaskHub", client.TaskHubName);
            }
        }

        public static class LegacyClientFunctions
        {
            [NoAutomaticTrigger]
#pragma warning disable CS0618 // Exercise the supported legacy binding.
            public static void Run([OrchestrationClient] IDurableOrchestrationClient client)
#pragma warning restore CS0618
            {
            }
        }

        public static class ClientOutputFunctions
        {
            [NoAutomaticTrigger]
#pragma warning disable DF0203 // Exercise the host's output binding, not an in-process client parameter.
            public static void Run([DurableClient] IAsyncCollector<StartOrchestrationArgs> output)
#pragma warning restore DF0203
            {
            }
        }

        public static class OutOfProcessClientFunctions
        {
            [NoAutomaticTrigger]
#pragma warning disable DF0203 // Exercise the string binding used by out-of-process workers.
            public static void Run([DurableClient] string client)
#pragma warning restore DF0203
            {
            }
        }

        public static class OverriddenClientFunctions
        {
            [NoAutomaticTrigger]
            public static void Run([DurableClient(TaskHub = "AnotherTaskHub")] IDurableClient client)
            {
            }
        }
    }
}
