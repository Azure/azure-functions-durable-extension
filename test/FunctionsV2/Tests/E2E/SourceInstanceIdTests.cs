// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class SourceInstanceIdTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public SourceInstanceIdTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RestartOrchestrator_ExposesSourceInstanceIdOnlyToTopLevelClone(bool extendedSessions)
        {
            using ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.RestartOrchestrator_ExposesSourceInstanceIdOnlyToTopLevelClone),
                enableExtendedSessions: extendedSessions);
            await host.StartAsync();

            const string FunctionName = nameof(TestOrchestrations.ReadSourceInstanceIdWithChild);
            TestDurableClient source = await host.StartOrchestratorAsync(FunctionName, null, this.output);
            DurableOrchestrationStatus sourceStatus = await source.WaitForCompletionAsync(this.output);
            Assert.Equal(OrchestrationRuntimeStatus.Completed, sourceStatus.RuntimeStatus);
            Assert.Equal(new string[] { null, null, null }, sourceStatus.Output.ToObject<string[]>());

            DateTime restartTime = DateTime.UtcNow;
            string cloneInstanceId = await source.InnerClient.RestartAsync(source.InstanceId, restartWithNewInstanceId: true);
            Assert.NotEqual(source.InstanceId, cloneInstanceId);
            var clone = new TestDurableClient(source.InnerClient, FunctionName, cloneInstanceId, restartTime);
            DurableOrchestrationStatus cloneStatus = await clone.WaitForCompletionAsync(this.output);

            Assert.Equal(OrchestrationRuntimeStatus.Completed, cloneStatus.RuntimeStatus);
            Assert.Equal(
                new[] { source.InstanceId, null, source.InstanceId },
                cloneStatus.Output.ToObject<string[]>());

            await host.StopAsync();
        }
    }
}
