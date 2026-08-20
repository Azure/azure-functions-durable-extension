// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class CurrentAppDetectionTests
    {
        private const string SecondaryStorageSettingName = "SecondaryStorage";

        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public CurrentAppDetectionTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task AzureStorage_CaseVariantTaskHub_DisabledOrchestratorIsRejected()
        {
            string taskHubName = TestHelpers.GetTaskHubNameFromTestName(
                nameof(this.AzureStorage_CaseVariantTaskHub_DisabledOrchestratorIsRejected),
                enableExtendedSessions: false);
            string caseVariantTaskHub = taskHubName.ToUpperInvariant();
            string instanceId = $"case-disabled-{Guid.NewGuid():N}";
            var nameResolver = new SimpleNameResolver(
                new Dictionary<string, string>
                {
                    { "TestTaskHub", caseVariantTaskHub },
                });

            try
            {
                using ITestHost host = TestHelpers.GetJobHost(
                    this.loggerProvider,
                    nameof(this.AzureStorage_CaseVariantTaskHub_DisabledOrchestratorIsRejected),
                    enableExtendedSessions: false,
                    nameResolver: nameResolver,
                    storageProviderType: TestHelpers.AzureStorageProviderType,
                    exactTaskHubName: taskHubName);
                await host.StartAsync();

                Exception exception = await Record.ExceptionAsync(
                    () => host.StartOrchestratorAsync(
                        nameof(TestOrchestrations.DisabledOrchestrator),
                        input: null,
                        this.output,
                        instanceId,
                        useTaskHubFromAppSettings: true));

                IDurableOrchestrationClient defaultClient =
                    await host.GetOrchestrationClientBindingTest(this.output);
                if (exception == null)
                {
                    DurableOrchestrationStatus persistedStatus =
                        await WaitForInstanceAsync(defaultClient, instanceId);
                    Assert.Fail(
                        $"The case-variant client accepted and persisted the disabled orchestration " +
                        $"with status {persistedStatus.RuntimeStatus}.");
                }

                FunctionInvocationException invocationException =
                    Assert.IsType<FunctionInvocationException>(exception);
                Assert.Contains(
                    "doesn't exist, is disabled, or is not an orchestrator function",
                    invocationException.InnerException?.ToString());
                await AssertInstanceRemainsAbsentAsync(defaultClient, instanceId);

                await host.StopAsync();
            }
            finally
            {
                await DeleteTaskHubAsync(taskHubName, TestHelpers.GetStorageConnectionString());
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task AzureStorage_DifferentConnection_SameTaskHub_ReachesTargetBackend()
        {
            string taskHubName = TestHelpers.GetTaskHubNameFromTestName(
                nameof(this.AzureStorage_DifferentConnection_SameTaskHub_ReachesTargetBackend),
                enableExtendedSessions: false);
            string instanceId = $"cross-account-{Guid.NewGuid():N}";
            string secondaryConnection = GetSecondaryStorageConnectionString();
            var nameResolver = new SimpleNameResolver(
                new Dictionary<string, string>
                {
                    { "TestTaskHub", taskHubName },
                    { SecondaryStorageSettingName, secondaryConnection },
                });
            var targetOptions = new DurableTaskOptions
            {
                HubName = taskHubName,
            };
            targetOptions.StorageProvider["ConnectionName"] = SecondaryStorageSettingName;

            try
            {
                using ITestHost sourceHost = TestHelpers.GetJobHost(
                    this.loggerProvider,
                    nameof(this.AzureStorage_DifferentConnection_SameTaskHub_ReachesTargetBackend),
                    enableExtendedSessions: false,
                    nameResolver: nameResolver,
                    storageProviderType: TestHelpers.AzureStorageProviderType,
                    exactTaskHubName: taskHubName,
                    types: new[] { typeof(ClientFunctions) });
                using ITestHost targetHost = TestHelpers.GetJobHostWithOptions(
                    this.loggerProvider,
                    targetOptions,
                    nameResolver: nameResolver,
                    types: new[] { typeof(CurrentAppDetectionFunctions) });

                await targetHost.StartAsync();
                await sourceHost.StartAsync();

                TestDurableClient targetClient = await this.StartOnSecondaryStorageAsync(
                    sourceHost,
                    nameof(CurrentAppDetectionFunctions.TargetOnlyOrchestrator),
                    input: "completed",
                    instanceId);
                DurableOrchestrationStatus targetStatus =
                    await targetClient.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, targetStatus.RuntimeStatus);
                Assert.Equal("target:completed", targetStatus.Output);

                IDurableOrchestrationClient sourceClient =
                    await sourceHost.GetOrchestrationClientBindingTest(this.output);
                await AssertInstanceRemainsAbsentAsync(sourceClient, instanceId);

                await sourceHost.StopAsync();
                await targetHost.StopAsync();
            }
            finally
            {
                await Task.WhenAll(
                    DeleteTaskHubAsync(taskHubName, TestHelpers.GetStorageConnectionString()),
                    DeleteTaskHubAsync(taskHubName, secondaryConnection));
            }
        }

        private static async Task AssertInstanceRemainsAbsentAsync(
            IDurableOrchestrationClient client,
            string instanceId)
        {
            int observations = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                Assert.Null(await client.GetStatusAsync(instanceId));
                observations++;
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(2));

            Assert.True(observations >= 3);
        }

        private async Task<TestDurableClient> StartOnSecondaryStorageAsync(
            ITestHost host,
            string functionName,
            object input,
            string instanceId)
        {
            var clientRef = new TestDurableClient[1];
            var arguments = new Dictionary<string, object>
            {
                { "functionName", functionName },
                { "instanceId", instanceId },
                { "input", input },
                { "clientRef", clientRef },
            };

            await host.CallAsync(
                typeof(ClientFunctions).GetMethod(nameof(ClientFunctions.StartFunctionWithConnection)),
                arguments);
            this.output.WriteLine($"Started {functionName}, Instance ID = {clientRef[0].InstanceId}");
            return clientRef[0];
        }

        private static async Task<DurableOrchestrationStatus> WaitForInstanceAsync(
            IDurableOrchestrationClient client,
            string instanceId)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
                if (status != null)
                {
                    return status;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(30));

            throw new TimeoutException($"Instance '{instanceId}' was accepted but did not persist.");
        }

        private static Task DeleteTaskHubAsync(string taskHubName, string connectionString)
        {
            var settings = new AzureStorageOrchestrationServiceSettings
            {
                TaskHubName = taskHubName,
                StorageAccountClientProvider = new StorageAccountClientProvider(connectionString),
            };
            return new AzureStorageOrchestrationService(settings).DeleteAsync();
        }

        private static string GetSecondaryStorageConnectionString()
        {
            string connectionString = Environment.GetEnvironmentVariable(SecondaryStorageSettingName);
            return !string.IsNullOrWhiteSpace(connectionString)
                ? connectionString
                : throw new InvalidOperationException(
                    $"The {SecondaryStorageSettingName} environment variable must point to the secondary Azurite instance.");
        }
    }
}
