// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests for <see cref="IDurabilityProviderFactory.SetRegisteredFunctions"/>.
    /// </summary>
    public class SetRegisteredFunctionsTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultInterfaceMethod_IsNoOp()
        {
            // A factory that does NOT override SetRegisteredFunctions should use
            // the default no-op implementation without throwing.
            IDurabilityProviderFactory factory = new NoOpDurabilityProviderFactory();

            factory.SetRegisteredFunctions(
                new[] { "Orch1", "Orch2" },
                new[] { "Activity1" },
                new[] { "Entity1" });

            // No exception = pass. The default method body is empty.
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DeregisteredFunctions_AreExcludedFromFilterList()
        {
            // Arrange: register some functions, then deregister a subset
            var extension = CreateExtension();

            var orch1 = new FunctionName("Orch1");
            var orch2 = new FunctionName("Orch2");
            var act1 = new FunctionName("Activity1");
            var act2 = new FunctionName("Activity2");
            var ent1 = new FunctionName("Entity1");
            var ent2 = new FunctionName("Entity2");
            var executor = new Mock<ITriggeredFunctionExecutor>().Object;

            extension.RegisterOrchestrator(orch1, new RegisteredFunctionInfo(executor, isOutOfProc: true));
            extension.RegisterOrchestrator(orch2, new RegisteredFunctionInfo(executor, isOutOfProc: true));
            extension.RegisterActivity(act1, executor);
            extension.RegisterActivity(act2, executor);
            extension.RegisterEntity(ent1, new RegisteredFunctionInfo(executor, isOutOfProc: true));
            extension.RegisterEntity(ent2, new RegisteredFunctionInfo(executor, isOutOfProc: true));

            // Deregister one of each type
            extension.DeregisterOrchestrator(orch2);
            extension.DeregisterActivity(act2);
            extension.DeregisterEntity(ent1);

            // Assert: only active (not deregistered) functions are included in
            // the names passed to SetRegisteredFunctions.
            var activeFunctions = extension.GetActiveRegisteredFunctionNames();

            Assert.Single(activeFunctions.orchestratorNames);
            Assert.Contains("Orch1", activeFunctions.orchestratorNames);
            Assert.DoesNotContain("Orch2", activeFunctions.orchestratorNames);

            Assert.Single(activeFunctions.activityNames);
            Assert.Contains("Activity1", activeFunctions.activityNames);
            Assert.DoesNotContain("Activity2", activeFunctions.activityNames);

            Assert.Single(activeFunctions.entityNames);
            Assert.Contains("Entity2", activeFunctions.entityNames);
            Assert.DoesNotContain("Entity1", activeFunctions.entityNames);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DisabledFunctions_AreExcludedFromFilterList()
        {
            // Reproduces https://github.com/Azure/azure-functions-durable-extension/issues/3448.
            // Binding providers register orchestrators/entities with a null RegisteredFunctionInfo
            // during indexing (see OrchestrationTriggerAttributeBindingProvider /
            // EntityTriggerAttributeBindingProvider). For functions disabled via attribute or
            // the AzureWebJobs.<Name>.Disabled app setting, the listener factory never runs,
            // so the null is never replaced. GetActiveRegisteredFunctionNames must (1) not
            // throw NullReferenceException on those entries, and (2) treat them as inactive,
            // matching the null-tolerant pattern used in StopTaskHubWorkerIfIdleAsync.
            var extension = CreateExtension();
            var executor = new Mock<ITriggeredFunctionExecutor>().Object;

            extension.RegisterOrchestrator(new FunctionName("DisabledOrch"), orchestratorInfo: null);
            extension.RegisterOrchestrator(new FunctionName("NullExecutorOrch"), new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));
            extension.RegisterOrchestrator(new FunctionName("ActiveOrch"), new RegisteredFunctionInfo(executor, isOutOfProc: true));

            // Activity indexing stores a non-null registration with a null executor.
            extension.RegisterActivity(new FunctionName("DisabledActivity"), executor: null!);
            extension.RegisterActivity(new FunctionName("ActiveActivity"), executor);

            extension.RegisterEntity(new FunctionName("DisabledEntity"), entityInfo: null);
            extension.RegisterEntity(new FunctionName("NullExecutorEntity"), new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));
            extension.RegisterEntity(new FunctionName("ActiveEntity"), new RegisteredFunctionInfo(executor, isOutOfProc: true));

            var activeFunctions = extension.GetActiveRegisteredFunctionNames();

            Assert.Equal(new[] { "ActiveOrch" }, activeFunctions.orchestratorNames);
            Assert.Equal(new[] { "ActiveActivity" }, activeFunctions.activityNames);
            Assert.Equal(new[] { "ActiveEntity" }, activeFunctions.entityNames);

            // Filtering capabilities must not remove indexed functions from scheduling validation.
            extension.ThrowIfFunctionDoesNotExist("DisabledActivity", FunctionType.Activity);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void NoActiveActivities_ProducesEmptyActivityList(bool deregister)
        {
            var extension = CreateExtension();
            var activityName = new FunctionName("OnlyActivity");
            extension.RegisterActivity(
                activityName,
                deregister ? new Mock<ITriggeredFunctionExecutor>().Object : null!);
            if (deregister)
            {
                extension.DeregisterActivity(activityName);
            }

            Assert.Empty(extension.GetActiveRegisteredFunctionNames().activityNames);
        }

        [Theory]
        [InlineData(0, 1, 2, false)]
        [InlineData(0, 2, 1, false)]
        [InlineData(1, 0, 2, false)]
        [InlineData(1, 2, 0, false)]
        [InlineData(2, 0, 1, false)]
        [InlineData(2, 1, 0, false)]
        [InlineData(0, 1, 2, true)]
        [InlineData(1, 0, 2, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task HostStartup_PassesAllActiveListenersToFactoryBeforeStartingWorker(
            int first, int second, int third, bool disableOnlyActivity)
        {
            Type[] functionTypes =
            [
                typeof(OrchestratorFunctions),
                typeof(ActivityFunctions),
                typeof(EntityFunctions),
            ];
            var typeLocator = new Mock<ITypeLocator>();
            typeLocator.Setup(locator => locator.GetTypes())
                .Returns(new[] { functionTypes[first], functionTypes[second], functionTypes[third] });

            var snapshots = new List<(string[] Orchestrators, string[] Activities, string[] Entities)>();
            var service = new Mock<IOrchestrationService>();
            service.SetupGet(s => s.MaxConcurrentTaskOrchestrationWorkItems).Returns(1);
            service.SetupGet(s => s.MaxConcurrentTaskActivityWorkItems).Returns(1);
            int snapshotsAtWorkerStart = 0;
            service.Setup(s => s.StartAsync())
                .Callback(() => snapshotsAtWorkerStart = snapshots.Count)
                .Returns(Task.CompletedTask);

            // No dispatcher pumps are needed: this test exercises actual WebJobs indexing and
            // listener startup, but captures provider capabilities without external storage.
            var provider = new DurabilityProvider(
                "FilterTest", service.Object, new Mock<IOrchestrationServiceClient>().Object, "TestConnection");
            var factory = new Mock<IDurabilityProviderFactory>();
            factory.Setup(f => f.Name).Returns("FilterTest");
            factory.Setup(f => f.GetDurabilityProvider()).Returns(provider);
            factory.Setup(f => f.SetRegisteredFunctions(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<IReadOnlyCollection<string>>()))
                .Callback<IReadOnlyCollection<string>, IReadOnlyCollection<string>, IReadOnlyCollection<string>>(
                    (orchestrators, activities, entities) =>
                        snapshots.Add((orchestrators.ToArray(), activities.ToArray(), entities.ToArray())));

            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureWebJobs.SettingDisabledActivity.Disabled"] = "true",
                    ["AzureWebJobs.EnabledActivity.Disabled"] = disableOnlyActivity.ToString(),
                }))
                .ConfigureWebJobs(builder => builder.AddDurableTask(options =>
                {
                    options.HubName = "FilterStartupTest";
                    options.StorageProvider["type"] = factory.Object.Name;
                    options.LocalRpcEndpointEnabled = false;
                    options.WebhookUriProviderOverride = () => new Uri("https://localhost");
                }))
                .ConfigureServices(services =>
                {
                    services.AddSingleton(typeLocator.Object);
                    services.AddSingleton(factory.Object);
                    services.AddSingleton(TestHelpers.GetMockPlatformInformationService());
                })
                .Build();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await host.StartAsync(timeout.Token);
            try
            {
                var snapshot = Assert.Single(snapshots);
                Assert.Equal(1, snapshotsAtWorkerStart);
                Assert.Equal(new[] { "EnabledOrchestrator" }, snapshot.Orchestrators);
                Assert.Equal(disableOnlyActivity ? Array.Empty<string>() : new[] { "EnabledActivity" }, snapshot.Activities);
                Assert.Equal(new[] { "EnabledEntity" }, snapshot.Entities);
                service.Verify(s => s.StartAsync(), Times.Once);
            }
            finally
            {
                using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await host.StopAsync(stopTimeout.Token);
            }
        }

        private static DurableTaskExtension CreateExtension()
        {
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };

            return new DurableTaskExtension(
                new OptionsWrapper<DurableTaskOptions>(options),
                NullLoggerFactory.Instance,
                TestHelpers.GetTestNameResolver(),
                [
                    new AzureStorageDurabilityProviderFactory(
                        new OptionsWrapper<DurableTaskOptions>(options),
                        new TestStorageServiceClientProviderFactory(),
                        TestHelpers.GetTestNameResolver(),
                        NullLoggerFactory.Instance,
                        TestHelpers.GetMockPlatformInformationService()),
                ],
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService());
        }

        public static class OrchestratorFunctions
        {
            [FunctionName("EnabledOrchestrator")]
            public static void Enabled([OrchestrationTrigger] IDurableOrchestrationContext context) { }

            [Disable]
            [FunctionName("DisabledOrchestrator")]
            public static void Disabled([OrchestrationTrigger] IDurableOrchestrationContext context) { }
        }

        public static class ActivityFunctions
        {
            [FunctionName("EnabledActivity")]
            public static void Enabled([ActivityTrigger] string input) { }

            [Disable]
            [FunctionName("AttributeDisabledActivity")]
            public static void AttributeDisabled([ActivityTrigger] string input) { }

            [FunctionName("SettingDisabledActivity")]
            public static void SettingDisabled([ActivityTrigger] string input) { }
        }

#pragma warning disable DF0305 // Function-based entities do not dispatch to an entity class.
        public static class EntityFunctions
        {
            [FunctionName("EnabledEntity")]
            public static void Enabled([EntityTrigger] IDurableEntityContext context) { }

            [Disable]
            [FunctionName("DisabledEntity")]
            public static void Disabled([EntityTrigger] IDurableEntityContext context) { }
        }
#pragma warning restore DF0305

        /// <summary>
        /// A minimal factory that does NOT override SetRegisteredFunctions,
        /// exercising the default interface method.
        /// </summary>
        private class NoOpDurabilityProviderFactory : IDurabilityProviderFactory
        {
            public string Name => "NoOpProvider";

            public DurabilityProvider GetDurabilityProvider()
                => throw new System.NotImplementedException();

            public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
                => throw new System.NotImplementedException();
        }
    }
}
