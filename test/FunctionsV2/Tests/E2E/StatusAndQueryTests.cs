// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class StatusAndQueryTests : DurableTaskEndToEndTestBase
    {
        public StatusAndQueryTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task StartOrchestration_OnUnregisteredOrchestrator(bool extendedSessions, string storageProvider)
        {
            const string activityFunctionName = "UnregisteredOrchestrator";
            string errorMessage = $"The function '{activityFunctionName}' doesn't exist, is disabled, or is not an orchestrator function";

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.StartOrchestration_OnUnregisteredOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                Exception ex = await Assert.ThrowsAsync<FunctionInvocationException>(async () => await host.StartOrchestratorAsync("UnregisteredOrchestrator", "Unregistered", this.output));

                Assert.NotNull(ex.InnerException);
                Assert.Contains(errorMessage, ex.InnerException?.ToString());

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DisabledOrchestrator_IsRejectedWithoutPersistingInstance()
        {
            const string InstanceId = "disabled-orchestrator-instance";
            string taskHubName = TestHelpers.GetTaskHubNameFromTestName(
                nameof(this.DisabledOrchestrator_IsRejectedWithoutPersistingInstance),
                enableExtendedSessions: false);
            var nameResolver = new SimpleNameResolver(
                new Dictionary<string, string>
                {
                    { "TestTaskHub", taskHubName },
                });

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DisabledOrchestrator_IsRejectedWithoutPersistingInstance),
                enableExtendedSessions: false,
                nameResolver: nameResolver,
                storageProviderType: TestHelpers.AzureStorageProviderType,
                exactTaskHubName: taskHubName))
            {
                await host.StartAsync();

                Exception exception = await Record.ExceptionAsync(
                    () => host.StartOrchestratorAsync(
                        nameof(TestOrchestrations.DisabledOrchestrator),
                        input: null,
                        this.output,
                        instanceId: InstanceId,
                        useTaskHubFromAppSettings: true));

                IDurableOrchestrationClient defaultClient =
                    await host.GetOrchestrationClientBindingTest(this.output);
                Assert.Null(await defaultClient.GetStatusAsync(InstanceId));

                FunctionInvocationException invocationException =
                    Assert.IsType<FunctionInvocationException>(exception);
                Assert.NotNull(invocationException.InnerException);
                Assert.Contains(
                    "doesn't exist, is disabled, or is not an orchestrator function",
                    invocationException.InnerException.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which creates an external client that calls a non-existent orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ExternalClient_CallsNonexistentOrchestrator(string storageProvider)
        {
            string taskHubName = TestHelpers.GetTaskHubNameFromTestName(
                nameof(this.ExternalClient_CallsNonexistentOrchestrator),
                enableExtendedSessions: false);

            Dictionary<string, string> appSettings = new Dictionary<string, string>
            {
                { "CustomStorageAccountName", TestHelpers.GetStorageConnectionString() },
                { "TestTaskHub", taskHubName },
            };

            // ConnectionName is used to look up the storage connection string in appsettings
            DurableClientOptions durableClientOptions = new DurableClientOptions
            {
                ConnectionName = "CustomStorageAccountName",
                TaskHub = taskHubName,
            };

            var clientProviderFactory = new CustomStorageServiceClientProviderFactory(appSettings);

            using (IHost clientHost = TestHelpers.GetJobHostExternalEnvironment(clientProviderFactory))
            {
                using (var orchestrationHost = TestHelpers.GetJobHost(
                   this.loggerProvider,
                   nameof(this.ExternalClient_CallsNonexistentOrchestrator),
                   enableExtendedSessions: false,
                   storageProviderType: storageProvider,
                   exactTaskHubName: taskHubName))
                {
                    await clientHost.StartAsync();
                    await orchestrationHost.StartAsync();

                    IDurableClientFactory durableClientFactory = clientHost.Services.GetRequiredService<IDurableClientFactory>();
                    IDurableClient durableClient = durableClientFactory.CreateClient(durableClientOptions);

                    string instanceId = await durableClient.StartNewAsync("NonexistentOrchestrator");

                    // Poll for the orchestration to fail rather than using a fixed delay
                    DurableOrchestrationStatus status = null;
                    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                    while (DateTime.UtcNow < deadline)
                    {
                        status = await durableClient.GetStatusAsync(instanceId);
                        if (status != null && status.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                        {
                            break;
                        }

                        await Task.Delay(200);
                    }

                    Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                    await orchestrationHost.StopAsync();
                    await clientHost.StopAsync();
                }
            }
        }

        /// <summary>
        /// End-to-end test which tests renaming/disabling/deleting activity functions. An orchestrator function schedules activity functions
        /// in the first host. The second host is created without any activity functions and an external client gets the status of the orchestrator
        /// instance. The orchestrator instance should fail in this case.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task NonexistentActivity_OrchestratorFunctionFails(bool extendedSessions, string storageProvider)
        {
            var modifiedTypeArray = new Type[]
            {
                typeof(TestOrchestrations),
                typeof(ClientFunctions),
            };

            string instanceId = "";
            string taskHub = "";
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.NonexistentActivity_OrchestratorFunctionFails),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivityWithDelay), null, this.output);
                instanceId = client.InstanceId;
                taskHub = client.TaskHubName;

                await client.WaitForStartupAsync(this.output, Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(20));
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                await host.StopAsync();
            }

            Dictionary<string, string> taskHubAndStorageAppSetting = new Dictionary<string, string>
            {
                { "CustomStorageAccountName", TestHelpers.GetStorageConnectionString() },
                { "TestTaskHub", taskHub },
            };

            var clientProviderFactory = new CustomStorageServiceClientProviderFactory(taskHubAndStorageAppSetting);

            // create a new host without activity functions and see if the function fails
            using (ITestHost newHost = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.NonexistentActivity_OrchestratorFunctionFails),
                extendedSessions,
                storageProviderType: storageProvider,
                exactTaskHubName: taskHub,
                types: modifiedTypeArray))
            {
                await newHost.StartAsync();
                using (IHost clientHost = TestHelpers.GetJobHostExternalEnvironment(clientProviderFactory))
                {
                    DurableClientOptions durableClientOptions = new DurableClientOptions
                    {
                        ConnectionName = "CustomStorageAccountName",
                        TaskHub = taskHub,
                    };

                    // create a new client (external)
                    await clientHost.StartAsync();
                    IDurableClientFactory durableClientFactory = clientHost.Services.GetRequiredService<IDurableClientFactory>();
                    IDurableClient durableClient = durableClientFactory.CreateClient(durableClientOptions);

                    // Poll for the orchestration to fail rather than using a fixed delay
                    DurableOrchestrationStatus newStatus = null;
                    var failDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                    while (DateTime.UtcNow < failDeadline)
                    {
                        newStatus = await durableClient.GetStatusAsync(instanceId);
                        if (newStatus?.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                        {
                            break;
                        }

                        await Task.Delay(200);
                    }

                    if (newStatus == null)
                    {
                        Assert.Fail("Orchestration status did not become available or fail within the expected time window.");
                        return;
                    }

                    Assert.Equal(OrchestrationRuntimeStatus.Failed, newStatus.RuntimeStatus);
                    Assert.Contains("Non-Deterministic workflow detected", newStatus.Output.ToString());
                    await clientHost.StopAsync();
                }

                await newHost.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_OnUnregisteredActivity(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallActivity),
            };

            const string activityFunctionName = "UnregisteredActivity";
            string errorMessage = $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The function '{activityFunctionName}' doesn't exist, is disabled, or is not an activity function";

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnUnregisteredActivity),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = activityFunctionName,
                    Input = new { Foo = "Bar" },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
                Assert.StartsWith(errorMessage, (string)status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnUnregisteredActivity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which runs an orchestrator function that calls another orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_OnValidOrchestrator(bool extendedSessions, string storageProvider)
        {
            const string greetingName = "ValidOrchestrator";
            const string validOrchestratorName = "SayHelloWithActivity";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                validOrchestratorName,
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            var input = new { Foo = greetingName };
            var inputJson = JsonConvert.SerializeObject(input);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnValidOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string parentInstanceId = "PARENT_" + Guid.NewGuid().ToString("N");
                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = orchestratorFunctionNames[1],
                    InstanceId = parentInstanceId + ":0",
                    Input = inputJson,
                };

                // Function type call chain: 'CallActivity' (orchestrator) -> 'SayHelloWithActivity' (orchestrator) -> 'Hello' (activity)
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output, parentInstanceId);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                var statusInput = JsonConvert.DeserializeObject<Dictionary<string, object>>(status.Input.ToString());

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(validOrchestratorName, statusInput["FunctionName"].ToString());
                Assert.Contains(greetingName, statusInput["Input"].ToString());
                Assert.Equal($"Hello, {inputJson}!", status.Output.ToString());

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnValidOrchestrator",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HandleUncallableOrchestrator(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandleUncallableOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(UnconstructibleClass.UncallableOrchestrator), null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
                Assert.Equal("Orchestrator function 'UncallableOrchestrator' failed: Exception of type 'System.Exception' was thrown.", status.Output.ToString());

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HandleUncallableFunctions(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandleUncallableFunctions),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(UnconstructibleClass.UncallableEntity), Guid.NewGuid().ToString());
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.HandleUncallableFunctions), entityId, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_OnUnregisteredOrchestrator(bool extendedSessions, string storageProvider)
        {
            const string unregisteredOrchestrator = "UnregisteredOrchestrator";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                unregisteredOrchestrator,
            };

            string errorMessage = $"The function '{unregisteredOrchestrator}' doesn't exist, is disabled, or is not an orchestrator function";

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnUnregisteredOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = unregisteredOrchestrator,
                    Input = new { Foo = "Bar" },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
                Assert.Contains(errorMessage, status.Output.ToString());

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnUnregisteredOrchestrator",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task AzureStorage_BigReturnValue_Orchestrator(bool extendedSessions, bool autoFetch)
        {
            string taskHub = nameof(this.AzureStorage_BigReturnValue_Orchestrator);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions, autoFetchLargeMessages: autoFetch))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.BigReturnValue);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;

                var client = await host.StartOrchestratorAsync(orchestrator, stringLength, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                if (!autoFetch)
                {
                    await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);
                }
                else
                {
                    Assert.Equal(stringLength, ((string)status.Output).Length);
                }

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task AzureStorage_BigReturnValue_Activity(bool extendedSessions, bool autoFetch)
        {
            string taskHub = nameof(this.AzureStorage_BigReturnValue_Activity);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions, autoFetchLargeMessages: autoFetch))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.CallActivity);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;
                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestActivities.BigReturnValue),
                    Input = stringLength,
                };

                var client = await host.StartOrchestratorAsync(orchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                if (!autoFetch)
                {
                    await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);
                }
                else
                {
                    Assert.Equal(stringLength, ((string)status.Output).Length);
                }

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task RaiseEventToSubOrchestration(bool extendedSessions, string storageProvider)
        {
            string taskHub = nameof(this.RaiseEventToSubOrchestration);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.Approval),
                    InstanceId = "SubOrchestration-" + Guid.NewGuid().ToString("N"),
                    Input = TimeSpan.FromMinutes(5),
                };

                var client = await host.StartOrchestratorAsync(orchestrator, input, this.output);
                var status = await client.WaitForStartupAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

                // Wait for the sub-orchestration to be started and waiting for input.
                await Task.Delay(TimeSpan.FromSeconds(10));
                await client.InnerClient.RaiseEventAsync(input.InstanceId, "approval", true);

                status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal("Approved", status.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SetStatusOrchestration(bool extendedSessions, string storageProvider)
        {
            const string testName = nameof(this.SetStatusOrchestration);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SetStatus), null, this.output);
                await client.WaitForStartupAsync(this.output);

                DurableOrchestrationStatus orchestrationStatus = await client.GetStatusAsync();
                Assert.Equal(JTokenType.Null, orchestrationStatus.CustomStatus?.Type);

                // The orchestrator will wait for an external event, and use the payload to update its custom status.
                const string statusValue = "updated status";
                await client.RaiseEventAsync("UpdateStatus", statusValue, this.output);
                await client.WaitForCustomStatusAsync(TimeSpan.FromSeconds(10), this.output, statusValue);

                // Test clearing an existing custom status
                await client.RaiseEventAsync("UpdateStatus", null, this.output);
                await client.WaitForCustomStatusAsync(TimeSpan.FromSeconds(30), this.output, JValue.CreateNull());

                // Test setting the custom status to a complex object.
                var newCustomStatus = new { Foo = "Bar", Count = 2, };
                await client.RaiseEventAsync("UpdateStatus", newCustomStatus, this.output);
                orchestrationStatus = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(orchestrationStatus);
                Assert.Equal(newCustomStatus.Foo, (string)orchestrationStatus?.CustomStatus["Foo"]);
                Assert.Equal(newCustomStatus.Count, (int)orchestrationStatus?.CustomStatus["Count"]);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, orchestrationStatus?.RuntimeStatus);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_InstanceNotFound(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.GetStatus_InstanceNotFound), false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Start a dummy orchestration just to help us get a client object
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloInline), null, this.output);
                await client.WaitForCompletionAsync(this.output);

                string bogusInstanceId = "BOGUS_" + Guid.NewGuid().ToString("N");
                this.output.WriteLine($"Fetching status for fake instance: {bogusInstanceId}");
                DurableOrchestrationStatus status = await client.InnerClient.GetStatusAsync(instanceId: bogusInstanceId);
                Assert.Null(status);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_ShowInputFalse(string storageProvider)
        {
            const string testName = nameof(this.GetStatus_ShowInputFalse);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, testName, false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 1, this.output);

                DurableOrchestrationStatus status = await client.GetStatusAsync(showHistory: false, showHistoryOutput: false, showInput: false);
                Assert.True(string.IsNullOrEmpty(status.Input.ToString()));
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_ShowInputDefault(string storageProvider)
        {
            const string testName = nameof(this.GetStatus_ShowInputDefault);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, testName, false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 1, this.output);

                DurableOrchestrationStatus status = await client.GetStatusAsync(showHistory: false, showHistoryOutput: false);
                Assert.Equal("1", status.Input.ToString());
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Deserialize_DurableOrchestrationStatus(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.Deserialize_DurableOrchestrationStatus), false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                DurableOrchestrationStatus input = new DurableOrchestrationStatus();
                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.GetDurableOrchestrationStatus),
                    input,
                    this.output);
                DurableOrchestrationStatus desereliazedStatus = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(desereliazedStatus);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, desereliazedStatus.RuntimeStatus);
                Assert.True(desereliazedStatus.LastUpdatedTime > desereliazedStatus.CreatedTime);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that Activity function can get an instance of HttpManagementPayload and return via the orchestrator.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Activity_Gets_HttpManagementPayload(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ReturnHttpManagementPayload),
                nameof(TestActivities.GetAndReturnHttpManagementPayload),
            };

            string testName = nameof(this.Activity_Gets_HttpManagementPayload);
            string taskHub = TestHelpers.GetTaskHubNameFromTestName(testName, extendedSessions);
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                exactTaskHubName: taskHub,
                notificationUrl: new Uri(TestConstants.NotificationUrl),
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                HttpManagementPayload httpManagementPayload = status.Output.ToObject<HttpManagementPayload>();
                ValidateHttpManagementPayload(httpManagementPayload, extendedSessions, taskHub);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates HttpManagementPayload retrieved from Orchestration client when executing a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task OrchestrationClient_Gets_HttpManagementPayload(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            string testName = nameof(this.OrchestrationClient_Gets_HttpManagementPayload);
            string taskHub = TestHelpers.GetTaskHubNameFromTestName(testName, extendedSessions);
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                notificationUrl: new Uri(TestConstants.NotificationUrl),
                storageProviderType: storageProvider,
                exactTaskHubName: taskHub))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                HttpManagementPayload httpManagementPayload = client.InnerClient.CreateHttpManagementPayload(status.InstanceId);
                ValidateHttpManagementPayload(httpManagementPayload, extendedSessions, taskHub);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Inline",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActorOrchestration_WithTaskHubName(bool extendedSessions, string storageProvider)
        {
            var taskHubName1 = "ActorOrchestration1";
            var taskHubName2 = "ActorOrchestration2";
            using (ITestHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions, storageProviderType: storageProvider))
            using (ITestHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions, storageProviderType: storageProvider))
            {
                await host1.StartAsync();
                await host2.StartAsync();

                int initialValue = 0;
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "World", this.output);
                var instanceId = client1.InstanceId;
                taskHubName1 = client1.TaskHubName;

                // Perform some operations
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);

                TimeSpan waitTimeout = TimeSpan.FromSeconds(10);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 1);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 3);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "decr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 3);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client1.GetStatusAsync();
                Assert.NotNull(status);
                Assert.True(
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                // The end message will cause the actor to complete itself.
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "end", this.output);

                status = await client1.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(3, (int)status.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.Equal(3, (int)status.Input);

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates legacy compatibility of orchestration and activity bindings.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task LegacyBaseClasses()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.LegacyBaseClasses),
                false))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.LegacyOrchestration), null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", (string)status.Output);

                await host.StopAsync();
            }
        }

        private static void ValidateHttpManagementPayload(HttpManagementPayload httpManagementPayload, bool extendedSessions, string taskHubName)
        {
            Assert.NotNull(httpManagementPayload);
            Assert.NotEmpty(httpManagementPayload.Id);
            string instanceId = httpManagementPayload.Id;
            string notificationUrl = TestConstants.NotificationUrlBase;

            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}?taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.StatusQueryGetUri);
            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}/raiseEvent/{{eventName}}?taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.SendEventPostUri);
            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}/terminate?reason={{text}}&taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.TerminatePostUri);
            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}/restart?taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.RestartPostUri);
        }
    }
}
