// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class ExceptionHandlingTests : DurableTaskEndToEndTestBase
    {
        public ExceptionHandlingTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HandledActivityException(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandledActivityException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Empty string input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.TryCatchLoop), 5, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(5, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the orchestrator's exception handling behavior.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task HandledSubOrchestratorException()
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandledActivityException),
                enableExtendedSessions: true))
            {
                await host.StartAsync();

                string message = $"Failure ID: {Guid.NewGuid()}";

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.SubOrchestrationThrow),
                    message,
                    this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
                Assert.Contains(message, (string)status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from orchestrator code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledOrchestrationException(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ThrowOrchestrator),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledOrchestrationException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = status.Output.ToString();
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith($"Orchestrator function '{orchestratorFunctionNames[0]}' failed: Value cannot be null.", output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledOrchestrationException",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates calling an orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_Activity(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorGreeting),
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_Activity),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(orchestratorFunctionNames[0], status.Name);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_Activity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ComplexType(string storageProvider)
        {
            await this.SubOrchestration_ComplexType_Main_Logic(nameof(this.SubOrchestration_ComplexType), storageProvider);
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ComplexType_History(string storageProvider)
        {
            await this.SubOrchestration_ComplexType_Main_Logic(nameof(this.SubOrchestration_ComplexType_History), storageProvider, showHistory: true);
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information with input and result data is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ComplexType_HistoryInputOutput(string storageProvider)
        {
            await this.SubOrchestration_ComplexType_Main_Logic(nameof(this.SubOrchestration_ComplexType_HistoryInputOutput), storageProvider, showHistory: true, showHistoryOutput: true);
        }

        /// <summary>
        /// End-to-end test which validates a sub-orchestrator function have assigned corrent value for <see cref="DurableOrchestrationContext.ParentInstanceId"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ParentInstanceId_Assigned(bool extendedSessions, string storageProvider)
        {
            const string TaskHub = nameof(this.SubOrchestration_ParentInstanceId_Assigned);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, TaskHub, extendedSessions, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.ProvideParentInstanceId),
                };

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);

                Assert.NotNull(status.Output);
                Assert.Equal(status.Output, client.InstanceId);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a sub-orchestrator function have assigned corrent value for <see cref="DurableOrchestrationContext.ParentInstanceId"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SubOrchestration_Requires_Different_Id_Than_Parent(bool extendedSessions)
        {
            const string TaskHub = nameof(this.SubOrchestration_Requires_Different_Id_Than_Parent);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, TaskHub, extendedSessions))
            {
                await host.StartAsync();

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);
                string instanceId = Guid.NewGuid().ToString();

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.ProvideParentInstanceId),
                    InstanceId = instanceId,
                };

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output, instanceId: instanceId);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from orchestrator functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledOrchestrationExceptionWithRetry(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorThrowWithRetry),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledOrchestrationExceptionWithRetry),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(50));

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                // Strip '\r' characters to make Windows and Unix output identical.
                string output = status.Output.ToString().Replace("\r", string.Empty);
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith($"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The orchestrator function 'ThrowOrchestrator' failed: \"Value cannot be null.", output);
                Assert.Contains("message", output);

                string subOrchestrationInstanceId = (string)status.CustomStatus;
                Assert.NotNull(subOrchestrationInstanceId);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledOrchestrationExceptionWithRetry",
                        subOrchestrationInstanceId,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an orchestrator function with null RetryOptions fails.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task OrchestrationWithRetry_NullRetryOptions(string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorWithRetry_NullRetryOptions),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationWithRetry_NullRetryOptions),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(50));

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = status.Output.ToString().Replace(Environment.NewLine, " ");
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: Value cannot be null.",
                    output);
                Assert.Contains("retryOptions", output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from activity code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledActivityException(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ThrowOrchestrator),
            };

            string activityFunctionName = nameof(TestActivities.ThrowActivity);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledActivityException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = (string)status.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The activity function '{activityFunctionName}' failed: \"{message}\"",
                    output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledActivityException",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from activity code
        /// within a sub-orchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledSubOrchestratorActivityException(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledSubOrchestratorActivityException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string exceptionMessage = "Kah-BOOOOM!!!";
                var args = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.ThrowOrchestrator),
                    Input = exceptionMessage,
                };

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.CallOrchestrator),
                    args,
                    this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = (string)status.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    string.Format(
                        "Orchestrator function '{0}' failed: The orchestrator function '{1}' failed: \"The activity function '{2}' failed: \"{3}\"",
                        nameof(TestOrchestrations.CallOrchestrator),
                        nameof(TestOrchestrations.ThrowOrchestrator),
                        nameof(TestActivities.ThrowActivity),
                        exceptionMessage),
                    output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledActivityExceptionWithRetry(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ActivityThrowWithRetry),
            };

            string activityFunctionName = nameof(TestActivities.ThrowActivity);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledActivityExceptionWithRetry),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(40));

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = (string)status.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The activity function '{activityFunctionName}' failed: \"{message}\"",
                    output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledActivityExceptionWithRetry",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an activity function with null RetryOptions fails.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActivityWithRetry_NullRetryOptions(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActivityWithRetry_NullRetryOptions),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                string orchestratorFunctionName = nameof(TestOrchestrations.ActivityWithRetry_NullRetryOptions);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionName, message, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(40));

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = (string)status.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.Contains(orchestratorFunctionName, output);
                Assert.Contains("Value cannot be null.", output);
                Assert.Contains("retryOptions", output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that waiting for an external event and calling
        /// an activity multiple times in a row does not lead to dropped events.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task WaitForEventAndCallActivity_DroppedEventsTest(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.WaitForEventAndCallActivity_DroppedEventsTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string orchestratorFunctionName = nameof(TestOrchestrations.WaitForEventAndCallActivity);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionName, null, this.output);

                for (int i = 0; i < 5; i++)
                {
                    await client.RaiseEventAsync("add", i, this.output);
                }

                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(60));

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                int output = (int)status.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.Equal(26, output);

                await host.StopAsync();
            }
        }

        private async Task SubOrchestration_ComplexType_Main_Logic(string taskHub, string storageProvider, bool showHistory = false, bool showHistoryOutput = false)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, enableExtendedSessions: false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var complexTypeDataInput = new ComplexType
                {
                    A = -42,
                    B = new List<DateTime> { DateTime.UtcNow, DateTime.UtcNow.AddYears(1) },
                    C = ComplexType.CustomEnum.Value2,
                    D = new ComplexType.ComplexInnerType
                    {
                        E = Guid.NewGuid().ToString(),
                        F = TimeSpan.FromHours(1.5),
                    },
                };

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.CallActivity),
                    Input = new StartOrchestrationArgs
                    {
                        FunctionName = nameof(TestActivities.Echo),
                        Input = complexTypeDataInput,
                    },
                };

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(
                    this.output,
                    showHistory,
                    showHistoryOutput);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);

                Assert.NotNull(status.Output);
                ComplexType complextTypeDataOutput = status.Output.ToObject<ComplexType>();
                CompareTwoComplexTypeObjects(complexTypeDataInput, complextTypeDataOutput);

                if (!showHistory)
                {
                    Assert.Null(status.History);
                }
                else
                {
                    Assert.Equal(3, status.History.Count);
                    Assert.Equal("ExecutionStarted", status.History[0]["EventType"].ToString());
                    Assert.Equal("CallOrchestrator", status.History[0]["FunctionName"].ToString());
                    Assert.Equal("SubOrchestrationInstanceCompleted", status.History[1]["EventType"].ToString());
                    Assert.Equal("CallActivity", status.History[1]["FunctionName"].ToString());
                    if (DateTime.TryParse(status.History[1]["Timestamp"].ToString(), out DateTime timestamp) &&
                        DateTime.TryParse(status.History[1]["FunctionName"].ToString(), out DateTime scheduledTime))
                    {
                        Assert.True(timestamp > scheduledTime);
                    }

                    Assert.Equal("ExecutionCompleted", status.History[2]["EventType"].ToString());
                    Assert.Equal("Completed", status.History[2]["OrchestrationStatus"].ToString());

                    if (showHistoryOutput)
                    {
                        Assert.NotNull(status.History[0]["Input"]);
                        Assert.NotNull(status.History[1]["Input"]);
                        Assert.NotNull(status.History[1]["Result"]);
                        var resultSubOrchestrationInstanceCompleted = JsonConvert.DeserializeObject<ComplexType>(status.History[1]["Result"].ToString());
                        CompareTwoComplexTypeObjects(complexTypeDataInput, resultSubOrchestrationInstanceCompleted);
                        Assert.NotNull(status.History[2]["Result"]);
                        var resultExecutionCompleted = JsonConvert.DeserializeObject<ComplexType>(status.History[2]["Result"].ToString());
                        CompareTwoComplexTypeObjects(complexTypeDataInput, resultExecutionCompleted);
                    }
                    else
                    {
                        Assert.NotNull(status.History[0]["Input"]);
                        Assert.Null(status.History[1]["Result"]);
                        Assert.Null(status.History[2]["Result"]);
                    }

                    Assert.NotNull(status.History);
                }

                await host.StopAsync();
            }
        }

        private static void CompareTwoComplexTypeObjects(ComplexType firstObject, ComplexType secondObject)
        {
            Assert.NotNull(secondObject);
            Assert.Equal(firstObject.A, secondObject.A);
            Assert.Equal(firstObject.B[0], secondObject.B[0]);
            Assert.Equal(firstObject.B[1], secondObject.B[1]);
            Assert.NotNull(secondObject.D);
            Assert.Equal(firstObject.D.E, secondObject.D.E);
            Assert.Equal(firstObject.D.F, secondObject.D.F);
        }
    }
}
