﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using DurableTask.AzureStorage.Monitoring;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.ScaleUtils;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class DurableTaskTargetScalerTests
    {
        private readonly DurableTaskTargetScaler targetScaler;
        private readonly TargetScalerContext scalerContext;
        private readonly Mock<DurableTaskMetricsProvider> metricsProviderMock;
        private readonly Mock<DurableTaskTriggerMetrics> triggerMetricsMock;
        private readonly Mock<IOrchestrationService> orchestrationServiceMock;
        private readonly Mock<DurabilityProvider> durabilityProviderMock;

        public DurableTaskTargetScalerTests(ITestOutputHelper output)
        {
            this.scalerContext = new TargetScalerContext();

            var loggerFactory = new LoggerFactory();
            var loggerProvider = new TestLoggerProvider(output);
            loggerFactory.AddProvider(loggerProvider);
            ILogger logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("DurableTask"));

            DisconnectedPerformanceMonitor nullPerformanceMonitorMock = null;
            CloudStorageAccount nullCloudStorageAccountMock = null;
            this.metricsProviderMock = new Mock<DurableTaskMetricsProvider>(
                MockBehavior.Strict,
                "FunctionName",
                "HubName",
                logger,
                nullPerformanceMonitorMock,
                nullCloudStorageAccountMock);

            this.triggerMetricsMock = new Mock<DurableTaskTriggerMetrics>(MockBehavior.Strict);
            this.orchestrationServiceMock = new Mock<IOrchestrationService>(MockBehavior.Strict);

            this.durabilityProviderMock = new Mock<DurabilityProvider>(
                MockBehavior.Strict,
                "storageProviderName",
                this.orchestrationServiceMock.Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "connectionName");

            this.targetScaler = new DurableTaskTargetScaler(
                "FunctionId",
                this.metricsProviderMock.Object,
                this.durabilityProviderMock.Object,
                logger);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(1, 10, 10, "[1, 1, 1, 1]", 10)]
        [InlineData(1, 10, 0, "[0, 0, 0, 0]", 0)]
        [InlineData(1, 10, 0, "[2, 2, 3, 3]", 1)]
        [InlineData(1, 10, 0, "[9999, 0, 0, 0]", 1)]
        [InlineData(1, 10, 0, "[9999, 0, 0, 1]", 2)]
        [InlineData(10, 10, 10, "[2, 2, 3, 3 ]", 1)]
        [InlineData(10, 10, 30, "[10, 10, 10, 1]", 4)]
        public async Task TestTargetScaler(int maxConcurrentActivities, int maxConcurrentOrchestrators, int workItemQueueLength, string controlQueueLengths, int expectedWorkerCount)
        {
            this.orchestrationServiceMock.SetupGet(m => m.MaxConcurrentTaskActivityWorkItems).Returns(maxConcurrentActivities);
            this.orchestrationServiceMock.SetupGet(m => m.MaxConcurrentTaskOrchestrationWorkItems).Returns(maxConcurrentOrchestrators);

            this.triggerMetricsMock.SetupGet(m => m.WorkItemQueueLength).Returns(workItemQueueLength);
            this.triggerMetricsMock.SetupGet(m => m.ControlQueueLengths).Returns(controlQueueLengths);

            this.metricsProviderMock.Setup(m => m.GetMetricsAsync()).ReturnsAsync(this.triggerMetricsMock.Object);

            var scaleResult = await this.targetScaler.GetScaleResultAsync(this.scalerContext);
            var targetWorkerCount = scaleResult.TargetWorkerCount;
            Assert.Equal(expectedWorkerCount, targetWorkerCount);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public void TestGetTargetScaler(bool supportsTBS)
        {
            ITargetScaler targetScaler = new Mock<ITargetScaler>().Object;
            this.durabilityProviderMock.Setup(m => m.TryGetTargetScaler(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), out targetScaler))
                .Returns(supportsTBS);

            var scaler = ScaleUtils.GetTargetScaler(this.durabilityProviderMock.Object, "FunctionId", new FunctionName("FunctionName"), "connectionName", "HubName");
            if (supportsTBS) {
                Assert.IsType<NoOpTargetScaler>(scaler);
            }
            else
            {
                Assert.Equal(targetScaler, scaler);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public void TestGetScaleMonitor(bool supportsScaleMonitor)
        {
            IScaleMonitor scaleMonitor = new Mock<IScaleMonitor>().Object;
            this.durabilityProviderMock.Setup(m => m.TryGetScaleMonitor(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), out scaleMonitor))
                .Returns(supportsScaleMonitor);

            var monitor = ScaleUtils.GetScaleMonitor(this.durabilityProviderMock.Object, "FunctionId", new FunctionName("FunctionName"), "connectionName", "HubName");
            if (supportsScaleMonitor)
            {
                Assert.IsType<NoOpScaleMonitor>(monitor);
            }
            else
            {
                Assert.Equal(scaleMonitor, monitor);
            }
        }
    }
}
