// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using P = Microsoft.DurableTask.Protobuf;

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
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ActivityShim_ExportsScheduledRetryMetadataAsOptionalJsonString(bool hasRetryMetadata)
        {
            using DurableTaskExtension extension = CreateExtension();
            ITriggerBinding binding = await CreateActivityBindingAsync(extension);
            var metadata = hasRetryMetadata ? new RetryMetadata(2, 3, "retry-group") : null;
            var taskContext = CreateActivityTaskContext(metadata);
            ITriggerData? triggerData = null;
            var executor = new Mock<ITriggeredFunctionExecutor>();
            executor.Setup(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Returns(async (TriggeredFunctionData data, CancellationToken cancellation) =>
                {
                    var activityContext = Assert.IsType<DurableActivityContext>(data.TriggerValue);
                    Assert.Same(metadata, activityContext.RetryMetadata);
                    triggerData = await binding.BindAsync(activityContext, context: null!);
                    activityContext.SetOutput("activity-output");
                    return new FunctionResult(true);
                });
            var shim = new TaskActivityShim(
                extension, executor.Object, new Mock<IApplicationLifetimeWrapper>().Object, "TestActivity");

            Assert.Equal("\"activity-output\"", await shim.RunAsync(taskContext, "[\"activity-input\"]"));

            Assert.NotNull(triggerData);
            Assert.Equal("activity-input", await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("test-instance", triggerData.BindingData["instanceId"]);
            Assert.Equal(typeof(string), binding.BindingDataContract["durableActivityRetryMetadata"]);
            Assert.False(triggerData.BindingData.ContainsKey("retryContext"));
            if (hasRetryMetadata)
            {
                string payload = Assert.IsType<string>(triggerData.BindingData["durableActivityRetryMetadata"]);
                Assert.True(JToken.DeepEquals(
                    JObject.Parse("{\"AttemptNumber\":2,\"MaxAttempts\":3,\"RetryGroupId\":\"retry-group\"}"),
                    JObject.Parse(payload)));
            }
            else
            {
                Assert.False(triggerData.BindingData.ContainsKey("durableActivityRetryMetadata"));
            }

            executor.Verify(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ActivityShim_InvalidPresentRetryMetadataFailsBeforeCustomerInvocation()
        {
            using DurableTaskExtension extension = CreateExtension();
            var executor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            var shim = new TaskActivityShim(
                extension, executor.Object, new Mock<IApplicationLifetimeWrapper>().Object, "TestActivity");

            // Simulate an invalid producer bypassing the validated CLR constructor.
            var invalidMetadata = (RetryMetadata)RuntimeHelpers.GetUninitializedObject(typeof(RetryMetadata));

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => shim.RunAsync(CreateActivityTaskContext(invalidMetadata), "[null]"));

            Assert.Contains("Invalid durable activity retry metadata", error.Message);
            executor.VerifyNoOtherCalls();
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ActivityBinding_AdminInvocationHasNoRetryMetadata()
        {
            using DurableTaskExtension extension = CreateExtension();
            ITriggerBinding binding = await CreateActivityBindingAsync(extension);

            ITriggerData data = await binding.BindAsync("\"activity-input\"", context: null!);

            Assert.Equal("activity-input", await data.ValueProvider.GetValueAsync());
            Assert.False(data.BindingData.ContainsKey("durableActivityRetryMetadata"));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ActivityMiddleware_PreservesOrRejectsScheduledMetadataBeforeInvocation(bool hasRetryMetadata, bool invalid)
        {
            using DurableTaskExtension extension = CreateExtension();
            extension.ConfigureForGrpcProtocol("durable-functions", "4.0.0");
            ITriggerBinding binding = await CreateActivityBindingAsync(extension);
            RetryMetadata? metadata = !hasRetryMetadata ? null : invalid
                ? (RetryMetadata)RuntimeHelpers.GetUninitializedObject(typeof(RetryMetadata))
                : new RetryMetadata(3, 3, "retry-group");
            ITriggerData? triggerData = null;
            var executor = new Mock<ITriggeredFunctionExecutor>();
            executor.Setup(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Returns(async (TriggeredFunctionData data, CancellationToken cancellation) =>
                {
                    var activityContext = Assert.IsType<DurableActivityContext>(data.TriggerValue);
                    Assert.Same(metadata, activityContext.RetryMetadata);
                    triggerData = await binding.BindAsync(activityContext, context: null!);
                    activityContext.SetOutput("activity-output");
                    return new FunctionResult(true);
                });
            extension.RegisterActivity(new FunctionName("TestActivity"), executor.Object);
            var dispatchContext = new DispatchMiddlewareContext();
            dispatchContext.SetProperty(new OrchestrationInstance { InstanceId = "test-instance" });
            dispatchContext.SetProperty(new TaskScheduledEvent(7, "TestActivity", "")
            {
                Input = "\"activity-input\"",
                RetryMetadata = metadata,
            });
            var middleware = new OutOfProcMiddleware(extension);

            if (invalid)
            {
                InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => middleware.CallActivityAsync(dispatchContext, () => throw new InvalidOperationException("Unexpected dispatch.")));
                Assert.Contains("Invalid durable activity retry metadata", error.Message);
                executor.VerifyNoOtherCalls();
                return;
            }

            await middleware.CallActivityAsync(dispatchContext, () => throw new InvalidOperationException("Unexpected dispatch."));

            Assert.NotNull(triggerData);
            Assert.Equal(hasRetryMetadata, triggerData.BindingData.ContainsKey("durableActivityRetryMetadata"));
            if (hasRetryMetadata)
            {
                JObject payload = JObject.Parse(Assert.IsType<string>(triggerData.BindingData["durableActivityRetryMetadata"]));
                Assert.Equal(3, payload.Value<int>("AttemptNumber"));
                Assert.Equal(3, payload.Value<int>("MaxAttempts"));
                Assert.Equal("retry-group", payload.Value<string>("RetryGroupId"));
            }

            var completed = Assert.IsType<TaskCompletedEvent>(dispatchContext.GetProperty<ActivityExecutionResult>().ResponseEvent);
            Assert.Equal(7, completed.TaskScheduledId);
            Assert.Equal("\"activity-output\"", completed.Result);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OrchestrationBinding_ProtobufHistoryPreservesOptionalRetryMetadata(bool hasRetryMetadata, bool isReplay)
        {
            ITriggerBinding binding = await CreateOrchestrationBindingAsync();
            var events = new List<HistoryEvent>
            {
                new ExecutionStartedEvent(-1, null)
                {
                    Name = "TestOrchestrator",
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "test-instance" },
                },
                new TaskScheduledEvent(7, "Activity", "")
                {
                    RetryMetadata = hasRetryMetadata ? new RetryMetadata(2, 3, "retry-group") : null,
                },
            };
            var runtimeState = new OrchestrationRuntimeState(isReplay ? events : new List<HistoryEvent>());
            if (!isReplay)
            {
                foreach (HistoryEvent historyEvent in events)
                {
                    runtimeState.AddEvent(historyEvent);
                }
            }

            var context = new RemoteOrchestratorContext(
                runtimeState,
                entityParameters: null,
                new DurableTaskOptions(),
                isExtendedSession: false,
                includePastEvents: true);

            ITriggerData data = await binding.BindAsync(context, context: null!);
            P.OrchestratorRequest wire = P.OrchestratorRequest.Parser.ParseFrom(
                Convert.FromBase64String(Assert.IsType<string>(await data.ValueProvider.GetValueAsync())));
            var wireEvents = isReplay ? wire.PastEvents : wire.NewEvents;
            P.TaskScheduledEvent scheduled = wireEvents[1].TaskScheduled;

            if (hasRetryMetadata)
            {
                Assert.Equal(2, scheduled.RetryMetadata.AttemptNumber);
                Assert.Equal(3, scheduled.RetryMetadata.MaxAttempts);
                Assert.Equal("retry-group", scheduled.RetryMetadata.RetryGroupId);
            }
            else
            {
                Assert.Null(scheduled.RetryMetadata);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OrchestrationBinding_LegacyHistoryPreservesOptionalRetryMetadata(bool hasRetryMetadata)
        {
            using DurableTaskExtension extension = CreateExtension();
            var provider = new OrchestrationTriggerAttributeBindingProvider(
                extension, "AzureWebJobsStorage", TestHelpers.GetMockPlatformInformationService());
            var providerContext = new TriggerBindingProviderContext(
                GetTriggerParameter(nameof(TestStringOrchestrator)), CancellationToken.None);
            ITriggerBinding binding = await provider.TryCreateAsync(providerContext)
                ?? throw new InvalidOperationException("The orchestration trigger binding was not created.");
            var durabilityProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "AzureWebJobsStorage");
            var context = new DurableOrchestrationContext(extension, durabilityProvider, "TestStringOrchestrator")
            {
                InstanceId = "test-instance",
                History = new List<HistoryEvent>
                {
                    new TaskScheduledEvent(7, "Activity", "")
                    {
                        RetryMetadata = hasRetryMetadata ? new RetryMetadata(1, int.MaxValue, "retry-group") : null,
                    },
                },
            };

            ITriggerData data = await binding.BindAsync(context, context: null!);
            JObject payload = JObject.Parse(Assert.IsType<string>(await data.ValueProvider.GetValueAsync()));
            JArray history = Assert.IsType<JArray>(payload["history"]);
            JObject scheduled = Assert.IsType<JObject>(history[0]);

            if (hasRetryMetadata)
            {
                JObject metadata = Assert.IsType<JObject>(scheduled["RetryMetadata"]);
                Assert.Equal(1, metadata.Value<int>("AttemptNumber"));
                Assert.Equal(int.MaxValue, metadata.Value<int>("MaxAttempts"));
                Assert.Equal("retry-group", metadata.Value<string>("RetryGroupId"));
            }
            else
            {
                Assert.Null(scheduled["RetryMetadata"]);
            }
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

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OrchestrationBinding_IncludesSourceInstanceId()
        {
            DurableTaskExtension extension = CreateExtension();
            var provider = new OrchestrationTriggerAttributeBindingProvider(
                extension,
                connectionName: "AzureWebJobsStorage",
                TestHelpers.GetMockPlatformInformationService());
            var providerContext = new TriggerBindingProviderContext(
                GetTriggerParameter(nameof(TestStringOrchestrator)),
                CancellationToken.None);
            ITriggerBinding binding = await provider.TryCreateAsync(providerContext)
                ?? throw new InvalidOperationException("The orchestration trigger binding was not created.");
            var durabilityProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "AzureWebJobsStorage");
            var orchestrationContext = new DurableOrchestrationContext(
                extension,
                durabilityProvider,
                "TestStringOrchestrator")
            {
                History = new List<HistoryEvent>(),
                InstanceId = "clone-instance",
                SourceInstanceId = "source-instance",
            };

            ITriggerData triggerData = await binding.BindAsync(orchestrationContext, context: null!);
            string payload = Assert.IsType<string>(await triggerData.ValueProvider.GetValueAsync());
            JObject json = JObject.Parse(payload);

            Assert.Equal("source-instance", (string?)json["sourceInstanceId"]);
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
        [InlineData("source-instance", false, true, false)]
        [InlineData("source-instance", false, true, true)]
        [InlineData("source-instance", false, false, true)]
        [InlineData("source-instance", true, true, false)]
        [InlineData("source-instance", true, true, true)]
        [InlineData("source-instance", true, false, true)]
        [InlineData(null, false, true, false)]
        [InlineData(null, false, true, true)]
        [InlineData(null, false, false, true)]
        [InlineData("", false, true, false)]
        [InlineData("", false, false, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OrchestrationBinding_ProtobufSourceInstanceId(
            string? sourceInstanceId,
            bool hasParent,
            bool includePastEvents,
            bool isReplay)
        {
            ITriggerBinding binding = await CreateOrchestrationBindingAsync();
            var startedEvent = new ExecutionStartedEvent(-1, null)
            {
                Name = "TestOrchestrator",
                OrchestrationInstance = new OrchestrationInstance
                {
                    InstanceId = "test-instance",
                    ExecutionId = "test-execution",
                },
                Tags = sourceInstanceId == null
                    ? null
                    : new Dictionary<string, string> { [DurableClient.SourceInstanceIdTag] = sourceInstanceId },
                ParentInstance = hasParent
                    ? new ParentInstance
                    {
                        Name = "Parent",
                        OrchestrationInstance = new OrchestrationInstance
                        {
                            InstanceId = "parent-instance",
                            ExecutionId = "parent-execution",
                        },
                    }
                    : null,
            };
            var runtimeState = new OrchestrationRuntimeState(
                isReplay ? new List<HistoryEvent> { startedEvent } : new List<HistoryEvent>());
            if (!isReplay)
            {
                runtimeState.AddEvent(startedEvent);
            }

            var remoteContext = new RemoteOrchestratorContext(
                runtimeState,
                entityParameters: null,
                new DurableTaskOptions { ExtendedSessionsEnabled = !includePastEvents },
                isExtendedSession: !includePastEvents,
                includePastEvents: includePastEvents);

            ITriggerData triggerData = await binding.BindAsync(remoteContext, context: null!);
            string payload = Assert.IsType<string>(await triggerData.ValueProvider.GetValueAsync());
            P.OrchestratorRequest request = P.OrchestratorRequest.Parser.ParseFrom(Convert.FromBase64String(payload));

            Assert.Equal("test-instance", request.InstanceId);
            Assert.Equal(includePastEvents && isReplay ? 1 : 0, request.PastEvents.Count);
            Assert.Equal(isReplay ? 0 : 1, request.NewEvents.Count);
            if (!hasParent && !string.IsNullOrEmpty(sourceInstanceId))
            {
                Assert.True(request.Properties.TryGetValue("sourceInstanceId", out var sourceInstanceProperty));
                Assert.Equal(sourceInstanceId, sourceInstanceProperty.StringValue);
            }
            else
            {
                Assert.False(request.Properties.ContainsKey("sourceInstanceId"));
            }
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

        private static async Task<ITriggerBinding> CreateActivityBindingAsync(DurableTaskExtension extension)
        {
            var provider = new ActivityTriggerAttributeBindingProvider(extension, "AzureWebJobsStorage");
            var providerContext = new TriggerBindingProviderContext(
                GetTriggerParameter(nameof(TestActivity)), CancellationToken.None);
            return await provider.TryCreateAsync(providerContext)
                ?? throw new InvalidOperationException("The activity trigger binding was not created.");
        }

        private static TaskContext CreateActivityTaskContext(RetryMetadata? metadata)
        {
            var context = new TaskContext(new OrchestrationInstance { InstanceId = "test-instance" });

            // Only the DTFx dispatcher can set this property in production.
            PropertyInfo property = typeof(TaskContext).GetProperty(nameof(TaskContext.RetryMetadata))
                ?? throw new InvalidOperationException("TaskContext.RetryMetadata was not found.");
            property.SetValue(context, metadata);
            return context;
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

        private static void TestStringOrchestrator(
            [OrchestrationTrigger] string context)
        {
        }

        private static void TestEntity(
            [EntityTrigger] IDurableEntityContext context)
        {
        }

        private static void TestActivity([ActivityTrigger] string input)
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
