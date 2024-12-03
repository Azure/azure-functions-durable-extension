// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.AzureStorage.Monitoring;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.ScaleUtils;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers;

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
        private readonly TestLoggerProvider loggerProvider;
        private readonly ITestOutputHelper output;

        public DurableTaskTargetScalerTests(ITestOutputHelper output)
        {
            this.scalerContext = new TargetScalerContext();
            this.output = output;
            var loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(this.output);
            loggerFactory.AddProvider(this.loggerProvider);
            ILogger logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("DurableTask"));

            DisconnectedPerformanceMonitor nullPerformanceMonitorMock = null;
            StorageAccountClientProvider storageAccountClientProvider = null;
            this.metricsProviderMock = new Mock<DurableTaskMetricsProvider>(
                MockBehavior.Strict,
                "HubName",
                logger,
                nullPerformanceMonitorMock,
                storageAccountClientProvider);

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
            if (!supportsTBS)
            {
                Assert.IsType<NoOpTargetScaler>(scaler);
                Assert.ThrowsAsync<NotSupportedException>(() => scaler.GetScaleResultAsync(context: null));
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
            if (!supportsScaleMonitor)
            {
                Assert.IsType<NoOpScaleMonitor>(monitor);
                Assert.Throws<InvalidOperationException>(() => monitor.GetScaleStatus(context: null));
            }
            else
            {
                Assert.Equal(scaleMonitor, monitor);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async void ScaleHostE2ETest(bool isTbsEnabled)
        {
            Action<ScaleOptions> configureScaleOptions = (scaleOptions) =>
            {
                scaleOptions.IsTargetScalingEnabled = isTbsEnabled;
                scaleOptions.MetricsPurgeEnabled = false;
                scaleOptions.ScaleMetricsMaxAge = TimeSpan.FromMinutes(4);
                scaleOptions.IsRuntimeScalingEnabled = true;
                scaleOptions.ScaleMetricsSampleInterval = TimeSpan.FromSeconds(1);
            };
            using (FunctionsV2HostWrapper host = (FunctionsV2HostWrapper)TestHelpers.GetJobHost(this.loggerProvider, nameof(this.ScaleHostE2ETest), enableExtendedSessions: false, configureScaleOptions: configureScaleOptions))
            {
                await host.StartAsync();

                IScaleStatusProvider scaleManager = host.InnerHost.Services.GetService<IScaleStatusProvider>();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 50, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(400));
                var scaleStatus = await scaleManager.GetScaleStatusAsync(new ScaleStatusContext());
                await host.StopAsync();
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                // We inspect the Host's logs for evidence that the Host is correctly sampling our scaling requests.
                // the expected logs depend on whether TBS is enabled or not
                var expectedSubString = "scale monitors to sample";
                if (isTbsEnabled)
                {
                    expectedSubString = "target scalers to sample";
                }

                bool containsExpectedLog = this.loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage ?? "").Any(p => p.Contains(expectedSubString));
                Assert.True(containsExpectedLog);
            }
        }
    }
}
