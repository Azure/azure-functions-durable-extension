// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestOrchestratorClient
    {
        private readonly DurableOrchestrationClient innerClient;
        private readonly string functionName;
        private readonly string instanceId;
        private readonly DateTime instanceCreationTime;

        internal TestOrchestratorClient(
            DurableOrchestrationClient innerClient,
            string functionName,
            string instanceId,
            DateTime instanceCreationTime)
        {
            this.innerClient = innerClient;
            this.functionName = functionName;
            this.instanceId = instanceId;
            this.instanceCreationTime = instanceCreationTime;
        }

        public string TaskHubName => this.innerClient.TaskHubName;

        public string FunctionName => this.functionName;

        public string InstanceId => this.instanceId;

        internal DurableOrchestrationClient InnerClient => this.innerClient;

        public async Task<DurableOrchestrationStatus> GetStatusAsync(bool showHistory = false, bool showHistoryOutput = false, bool showInput = true)
        {
            DurableOrchestrationStatus status = await this.innerClient.GetStatusAsync(this.instanceId, showHistory, showHistoryOutput, showInput);

            if (status != null)
            {
                // Validate the status before returning
                Assert.Equal(this.functionName, status.Name);
                Assert.Equal(this.instanceId, status.InstanceId);
                Assert.True(status.CreatedTime >= this.instanceCreationTime);
                Assert.True(status.CreatedTime <= DateTime.UtcNow);
                Assert.True(status.LastUpdatedTime >= status.CreatedTime);
                Assert.True(status.LastUpdatedTime <= DateTime.UtcNow);
            }

            return status;
        }

        public async Task RaiseEventAsync(string eventName, object eventData)
        {
            await this.innerClient.RaiseEventAsync(this.instanceId, eventName, eventData);
        }

        public async Task RaiseEventAsync(string taskHubName, string instanceid, string eventName, object eventData, string connectionName = null)
        {
            await this.innerClient.RaiseEventAsync(taskHubName, instanceid, eventName, eventData);
        }

        public async Task TerminateAsync(string reason)
        {
            await this.innerClient.TerminateAsync(this.instanceId, reason);
        }

        public async Task RewindAsync(string reason)
        {
            await this.innerClient.RewindAsync(this.instanceId, reason);
        }

        public async Task<DurableOrchestrationStatus> WaitForStartupAsync(TimeSpan timeout, ITestOutputHelper output)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                DurableOrchestrationStatus status = await this.GetStatusAsync();
                if (status != null && status.RuntimeStatus != OrchestrationRuntimeStatus.Pending)
                {
                    output.WriteLine($"{status.Name} (ID = {status.InstanceId}) started successfully after ~{sw.ElapsedMilliseconds}ms. Status = {status.RuntimeStatus}.");
                    return status;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            while (sw.Elapsed < timeout);

            throw new TimeoutException($"Durable function '{this.functionName}' with instance ID '{this.instanceId}' failed to start.");
        }

        public async Task<DurableOrchestrationStatus> WaitForCompletionAsync(TimeSpan timeout, ITestOutputHelper output, bool showHistory = false, bool showHistoryOutput = false)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                DurableOrchestrationStatus status = await this.GetStatusAsync(showHistory, showHistoryOutput);
                if (status?.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    output.WriteLine($"{status.Name} (ID = {status.InstanceId}) completed after ~{sw.ElapsedMilliseconds}ms. Status = {status.RuntimeStatus}. Output = {status.Output}.");
                    return status;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            while (sw.Elapsed < timeout);

            throw new TimeoutException($"Durable function '{this.functionName}' with instance ID '{this.instanceId}' failed to complete.");
        }
    }
}
