// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
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

        [Theory]
        [InlineData("orchestration", nameof(TestGrpcOrchestrator))]
        [InlineData("activity", nameof(TestGrpcActivity))]
        [InlineData("entity", nameof(TestGrpcEntity))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GrpcBindingMetadata_ReportsExactSdkIdentity(string bindingType, string methodName)
        {
            string hubName = $"SdkMetadataHub{Guid.NewGuid():N}";
            using var events = new SdkUsageEventListener(hubName);
            using DurableTaskExtension extension = CreateExtension(hubName, WorkerRuntimeType.Node);
            ITriggerBindingProvider provider = bindingType switch
            {
                "orchestration" => new OrchestrationTriggerAttributeBindingProvider(
                    extension,
                    connectionName: "AzureWebJobsStorage",
                    TestHelpers.GetMockPlatformInformationService()),
                "activity" => new ActivityTriggerAttributeBindingProvider(
                    extension,
                    connectionName: "AzureWebJobsStorage"),
                "entity" => new EntityTriggerAttributeBindingProvider(
                    extension,
                    connectionName: "AzureWebJobsStorage"),
                _ => throw new ArgumentOutOfRangeException(nameof(bindingType)),
            };
            var context = new TriggerBindingProviderContext(
                GetTriggerParameter(methodName),
                CancellationToken.None);

            Assert.NotNull(await provider.TryCreateAsync(context));

            EventWrittenEventArgs captured = Assert.Single(events.Events);
            Assert.Equal("durable-functions", captured.Payload![3]);
            Assert.Equal("4.0.0", captured.Payload[4]);
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
            return CreateExtension("TestHub", WorkerRuntimeType.Unknown);
        }

        private static DurableTaskExtension CreateExtension(string hubName, WorkerRuntimeType runtimeType)
        {
            var options = new DurableTaskOptions
            {
                HubName = hubName,
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };
            var platformInformation = TestHelpers.GetMockPlatformInformationService(language: runtimeType);

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
                        platformInformation),
                ],
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: platformInformation);
        }

        private static void TestOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
        }

        private static void TestEntity(
            [EntityTrigger] IDurableEntityContext context)
        {
        }

        private static void TestGrpcOrchestrator(
            [OrchestrationTrigger(
                DurableRequiresGrpc = true,
                DurableSdkName = "durable-functions",
                DurableSdkVersion = "4.0.0")]
            IDurableOrchestrationContext context)
        {
        }

        private static void TestGrpcActivity(
            [ActivityTrigger(
                DurableRequiresGrpc = true,
                DurableSdkName = "durable-functions",
                DurableSdkVersion = "4.0.0")]
            string input)
        {
        }

        private static void TestGrpcEntity(
            [EntityTrigger(
                DurableRequiresGrpc = true,
                DurableSdkName = "durable-functions",
                DurableSdkVersion = "4.0.0")]
            IDurableEntityContext context)
        {
        }
    }
}
