// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TriggerBindingProviderTests
    {
        public static IEnumerable<object?[]> DirectInvocationTriggerValues()
        {
            yield return new object?[] { null };
            yield return new object?[] { string.Empty };
        }

        public static IEnumerable<object?[]> UnsupportedTriggerValues()
        {
            yield return new object?[] { 42, "Int32" };
        }

        [Theory]
        [MemberData(nameof(DirectInvocationTriggerValues))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OrchestrationBinding_DirectInvocationValue_ThrowsDirectInvocationError(object? value)
        {
            ITriggerBinding binding = await CreateOrchestrationBindingAsync();

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => binding.BindAsync(value!, context: null!));

            Assert.Equal(
                "Durable orchestrator functions do not support direct invocation. " +
                "Start an orchestration from a client function by using a Durable client.",
                exception.Message);
        }

        [Theory]
        [MemberData(nameof(DirectInvocationTriggerValues))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task EntityBinding_DirectInvocationValue_ThrowsDirectInvocationError(object? value)
        {
            ITriggerBinding binding = await CreateEntityBindingAsync();

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => binding.BindAsync(value!, context: null!));

            Assert.Equal(
                "Durable entity functions do not support direct invocation. " +
                "Signal an entity from a client or orchestrator function by using a Durable client.",
                exception.Message);
        }

        [Theory]
        [MemberData(nameof(UnsupportedTriggerValues))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OrchestrationBinding_UnsupportedValue_PreservesTypeDiagnostic(
            object? value,
            string expectedType)
        {
            ITriggerBinding binding = await CreateOrchestrationBindingAsync();

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
                () => binding.BindAsync(value!, context: null!));

            Assert.Contains($"Don't know how to bind to {expectedType}.", exception.Message);
            Assert.Equal("value", exception.ParamName);
        }

        [Theory]
        [MemberData(nameof(UnsupportedTriggerValues))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task EntityBinding_UnsupportedValue_PreservesTypeDiagnostic(
            object? value,
            string expectedType)
        {
            ITriggerBinding binding = await CreateEntityBindingAsync();

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
                () => binding.BindAsync(value!, context: null!));

            Assert.Contains($"Don't know how to bind to {expectedType}.", exception.Message);
            Assert.Equal("value", exception.ParamName);
        }

        private static async Task<ITriggerBinding> CreateOrchestrationBindingAsync()
        {
            DurableTaskExtension extension = CreateExtension();
            var provider = new OrchestrationTriggerAttributeBindingProvider(
                extension,
                connectionName: "AzureWebJobsStorage",
                TestHelpers.GetMockPlatformInformationService());
            var context = new TriggerBindingProviderContext(
                GetTriggerParameter(nameof(TestOrchestrator)),
                CancellationToken.None);

            return await provider.TryCreateAsync(context)
                ?? throw new InvalidOperationException("The orchestration trigger binding was not created.");
        }

        private static async Task<ITriggerBinding> CreateEntityBindingAsync()
        {
            DurableTaskExtension extension = CreateExtension();
            var provider = new EntityTriggerAttributeBindingProvider(extension, connectionName: "AzureWebJobsStorage");
            var context = new TriggerBindingProviderContext(
                GetTriggerParameter(nameof(TestEntity)),
                CancellationToken.None);

            return await provider.TryCreateAsync(context)
                ?? throw new InvalidOperationException("The entity trigger binding was not created.");
        }

        private static ParameterInfo GetTriggerParameter(string methodName)
        {
            MethodInfo method = typeof(TriggerBindingProviderTests)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"The test method '{methodName}' was not found.");
            return method.GetParameters()[0];
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

        private static void TestOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
        }

        private static void TestEntity(
            [EntityTrigger] IDurableEntityContext context)
        {
        }
    }
}
