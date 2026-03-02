// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Tests for DurableTaskTargetScaler.
    /// Validates the target-based autoscaling mechanism for Durable Functions.
    /// Tests worker count calculations based on queue depths and concurrency limits.
    /// Ensures accurate scaling decisions for both orchestrators and activities.
    /// This is the primary scaling approach used by Azure Functions Scale Controller.
    /// </summary>
    public class DurableTaskTargetScalerTests : System.IDisposable
    {
        private readonly DurableTaskTargetScaler targetScaler;
        private readonly TargetScalerContext scalerContext;
        private readonly Mock<DurableTaskMetricsProvider> metricsProviderMock;
        private readonly Mock<DurableTaskTriggerMetrics> triggerMetricsMock;
        private readonly Mock<ScalabilityProvider> scalabilityProviderMock;
        private readonly TestLoggerProvider loggerProvider;
        private readonly ITestOutputHelper output;
        private readonly LoggerFactory loggerFactory;

        public DurableTaskTargetScalerTests(ITestOutputHelper output)
        {
            this.scalerContext = new TargetScalerContext();
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(this.output);
            this.loggerFactory.AddProvider(this.loggerProvider);
            ILogger logger = this.loggerFactory.CreateLogger<DurableTaskTargetScaler>();

            DisconnectedPerformanceMonitor nullPerformanceMonitorMock = null;
            StorageAccountClientProvider storageAccountClientProvider = null;
            this.metricsProviderMock = new Mock<DurableTaskMetricsProvider>(
                MockBehavior.Strict,
                "HubName",
                logger,
                nullPerformanceMonitorMock,
                storageAccountClientProvider);

            this.triggerMetricsMock = new Mock<DurableTaskTriggerMetrics>(MockBehavior.Strict);
            this.scalabilityProviderMock = new Mock<ScalabilityProvider>(MockBehavior.Strict, "AzureStorage", "TestConnection");
            this.scalabilityProviderMock.SetupGet(s => s.MaxConcurrentTaskActivityWorkItems).Returns(10);
            this.scalabilityProviderMock.SetupGet(s => s.MaxConcurrentTaskOrchestrationWorkItems).Returns(10);

            this.targetScaler = new DurableTaskTargetScaler(
                "FunctionId",
                this.metricsProviderMock.Object,
                this.scalabilityProviderMock.Object,
                logger);
        }

        public void Dispose()
        {
            this.loggerFactory?.Dispose();
        }

        [Theory]
        [InlineData(1, 10, 10, "[1, 1, 1, 1]", 10)]
        [InlineData(1, 10, 0, "[0, 0, 0, 0]", 0)]
        [InlineData(1, 10, 0, "[2, 2, 3, 3]", 1)]
        [InlineData(1, 10, 0, "[9999, 0, 0, 0]", 1)]
        [InlineData(1, 10, 0, "[9999, 0, 0, 1]", 2)]
        [InlineData(10, 10, 10, "[2, 2, 3, 3 ]", 1)]
        [InlineData(10, 10, 30, "[10, 10, 10, 1]", 4)]
        public async Task TestTargetScaler(int maxConcurrentActivities, int maxConcurrentOrchestrators, int workItemQueueLength, string controlQueueLengths, int expectedWorkerCount)
        {
            this.scalabilityProviderMock.SetupGet(m => m.MaxConcurrentTaskActivityWorkItems).Returns(maxConcurrentActivities);
            this.scalabilityProviderMock.SetupGet(m => m.MaxConcurrentTaskOrchestrationWorkItems).Returns(maxConcurrentOrchestrators);

            this.triggerMetricsMock.SetupGet(m => m.WorkItemQueueLength).Returns(workItemQueueLength);
            this.triggerMetricsMock.SetupGet(m => m.ControlQueueLengths).Returns(controlQueueLengths);

            this.metricsProviderMock.Setup(m => m.GetMetricsAsync()).ReturnsAsync(this.triggerMetricsMock.Object);

            var scaleResult = await this.targetScaler.GetScaleResultAsync(this.scalerContext);
            var targetWorkerCount = scaleResult.TargetWorkerCount;
            Assert.Equal(expectedWorkerCount, targetWorkerCount);
        }
    }
}
