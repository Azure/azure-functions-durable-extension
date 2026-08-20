// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using DurableTask.AzureStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

            extension.RegisterOrchestrator(orch1, new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));
            extension.RegisterOrchestrator(orch2, new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));
            extension.RegisterActivity(act1, executor: null!);
            extension.RegisterActivity(act2, executor: null!);
            extension.RegisterEntity(ent1, new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));
            extension.RegisterEntity(ent2, new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));

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

            extension.RegisterOrchestrator(new FunctionName("DisabledOrch"), orchestratorInfo: null);
            extension.RegisterOrchestrator(new FunctionName("ActiveOrch"), new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));

            // No "DisabledActivity" case: unlike RegisterOrchestrator / RegisterEntity (which
            // accept and store a possibly-null RegisteredFunctionInfo), RegisterActivity always
            // wraps its executor in a non-null RegisteredFunctionInfo, so knownActivities never
            // holds a null value through any legitimate code path. The production null-check on
            // activities exists for symmetry / defense-in-depth only and can't be exercised here
            // without reflection.
            extension.RegisterActivity(new FunctionName("ActiveActivity"), executor: null!);

            extension.RegisterEntity(new FunctionName("DisabledEntity"), entityInfo: null);
            extension.RegisterEntity(new FunctionName("ActiveEntity"), new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));

            var activeFunctions = extension.GetActiveRegisteredFunctionNames();

            Assert.Contains("ActiveOrch", activeFunctions.orchestratorNames);
            Assert.DoesNotContain("DisabledOrch", activeFunctions.orchestratorNames);

            Assert.Contains("ActiveActivity", activeFunctions.activityNames);

            Assert.Contains("ActiveEntity", activeFunctions.entityNames);
            Assert.DoesNotContain("DisabledEntity", activeFunctions.entityNames);
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
