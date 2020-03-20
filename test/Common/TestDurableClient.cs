// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestDurableClient
    {
        private readonly IDurableClient innerClient;
        private readonly string functionName;
        private readonly string instanceId;
        private readonly DateTime instanceCreationTime;

        internal TestDurableClient(
            IDurableClient innerClient,
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

        internal IDurableClient InnerClient => this.innerClient;

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

                // TODO: Emulator provider currently doesn't set status.LastUpdatedTime, so this assertion is not true.
                // Assert.True(status.LastUpdatedTime >= status.CreatedTime);

                Assert.True(status.LastUpdatedTime <= DateTime.UtcNow);
            }

            return status;
        }

        public Task<OrchestrationStatusQueryResult> GetStatusAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            return this.innerClient.ListInstancesAsync(condition, cancellationToken);
        }

        public async Task RaiseEventAsync(string eventName, ITestOutputHelper output)
        {
            output?.WriteLine($"Raising event {eventName} to {this.instanceId}.");
            await this.innerClient.RaiseEventAsync(this.instanceId, eventName);
        }

        public async Task RaiseEventAsync(string eventName, object eventData, ITestOutputHelper output)
        {
            output?.WriteLine($"Raising event {eventName} to {this.instanceId}. Payload: {eventData}");
            await this.innerClient.RaiseEventAsync(this.instanceId, eventName, eventData);
        }

        public async Task RaiseEventAsync(
            string taskHubName,
            string instanceid,
            string eventName,
            object eventData,
            ITestOutputHelper output,
            string connectionName = null)
        {
            output?.WriteLine($"Raising event {eventName} to {this.instanceId} in task hub {taskHubName}. Payload: {eventData}");
            await this.innerClient.RaiseEventAsync(
                taskHubName,
                instanceid,
                eventName,
                eventData,
                connectionName);
        }

        public async Task TerminateAsync(string reason)
        {
            await this.innerClient.TerminateAsync(this.instanceId, reason);
        }

        public async Task RewindAsync(string reason)
        {
#pragma warning disable 0618
            await this.innerClient.RewindAsync(this.instanceId, reason);
#pragma warning restore 0618
        }

        public async Task<DurableOrchestrationStatus> WaitForStartupAsync(ITestOutputHelper output, TimeSpan? timeout = null)
        {
            if (timeout == null)
            {
                // We wait up to 30 seconds for things to start. It shouldn't normally take this
                // long, but for whatever reason a small minority of tests don't acquire all
                // partitions in less than 10 seconds.
                timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
            }

            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                output.WriteLine($"Waiting for instance {this.instanceId} to start.");

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

        public async Task<DurableOrchestrationStatus> WaitForCompletionAsync(
            ITestOutputHelper output,
            bool showHistory = false,
            bool showHistoryOutput = false,
            TimeSpan? timeout = null)
        {
            if (timeout == null)
            {
                timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
            }

            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                output.WriteLine($"Waiting for instance {this.instanceId} to complete.");

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

        public async Task<DurableOrchestrationStatus> WaitForCustomStatusAsync(TimeSpan timeout, ITestOutputHelper output, JToken expectedStatus)
        {
            Stopwatch sw = Stopwatch.StartNew();
            DurableOrchestrationStatus status;
            do
            {
                output.WriteLine($"Waiting for {this.functionName} ({this.instanceId}) to have a custom status of {expectedStatus}.");

                status = await this.GetStatusAsync(showInput: false);
                if (status?.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    output.WriteLine($"{status.Name} (ID = {status.InstanceId}) completed after ~{sw.ElapsedMilliseconds}ms. Status = {status.RuntimeStatus}. Output = {status.Output}.");
                    break;
                }

                if (status.CustomStatus.Equals(expectedStatus))
                {
                    output.WriteLine($"{status.Name} ({status.InstanceId}) now shows a status of '{status.CustomStatus}'.");
                    return status;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            while (sw.Elapsed < timeout);

            output.WriteLine($"Timeout expired. {status.Name} ({status.InstanceId}) currently shows a status of '{status.CustomStatus}'.");
            Assert.Equal(expectedStatus.ToString(), status.CustomStatus?.ToString());
            return status;
        }
    }
}
