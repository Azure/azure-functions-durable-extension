// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskScaleMonitorTests
    {
        private readonly string functionId = "DurableTaskTriggerFunctionId";
        private readonly FunctionName functionName = new FunctionName("DurableTaskTriggerFunctionName");
        private readonly string hubName = "DurableTaskTriggerHubName";
        private readonly string storageConnectionString = TestHelpers.GetStorageConnectionString();
        private readonly ITestOutputHelper output;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly LoggerFactory loggerFactory;
        private readonly TestLoggerProvider loggerProvider;
        private readonly Mock<DisconnectedPerformanceMonitor> performanceMonitor;
        private readonly DurableTaskScaleMonitor scaleMonitor;

        public DurableTaskScaleMonitorTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);
            ILogger logger = this.loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("DurableTask"));
            this.traceHelper = new EndToEndTraceHelper(logger, false);
            this.performanceMonitor = new Mock<DisconnectedPerformanceMonitor>(MockBehavior.Strict, this.storageConnectionString, this.hubName);

            this.scaleMonitor = new DurableTaskScaleMonitor(
                this.functionId,
                this.functionName.Name,
                this.hubName,
                this.storageConnectionString,
                logger,
                this.performanceMonitor.Object);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ScaleMonitorDescriptor_ReturnsExpectedValue()
        {
            Assert.Equal($"{this.functionId}-DurableTaskTrigger-{this.hubName}".ToLower(), this.scaleMonitor.Descriptor.Id);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetMetrics_ReturnsExpectedResult()
        {
            PerformanceHeartbeat[] heartbeats;
            DurableTaskTriggerMetrics[] expectedMetrics;

            this.GetCorrespondingHeartbeatsAndMetrics(out heartbeats, out expectedMetrics);

            this.performanceMonitor
                .Setup(m => m.PulseAsync())
                .Returns(Task.FromResult(heartbeats[0]));

            var actualMetrics = await this.scaleMonitor.GetMetricsAsync();

            Assert.Equal(expectedMetrics[0].PartitionCount, actualMetrics.PartitionCount);
            Assert.Equal(expectedMetrics[0].ControlQueueLengths, actualMetrics.ControlQueueLengths);
            Assert.Equal(expectedMetrics[0].ControlQueueLatencies, actualMetrics.ControlQueueLatencies);
            Assert.Equal(expectedMetrics[0].WorkItemQueueLength, actualMetrics.WorkItemQueueLength);
            Assert.Equal(expectedMetrics[0].WorkItemQueueLatency, actualMetrics.WorkItemQueueLatency);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetMetrics_HandlesExceptions()
        {
            // StorageException
            var errorMsg = "Uh oh";
            this.performanceMonitor
                .Setup(m => m.PulseAsync())
                .Throws(new StorageException(errorMsg));

            var metrics = await this.scaleMonitor.GetMetricsAsync();

            var warning = this.loggerProvider.GetAllLogMessages().Last(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            var expectedWarning = $"Microsoft.WindowsAzure.Storage.StorageException: {errorMsg}";
            Assert.StartsWith(expectedWarning, warning.FormattedMessage);
        }

        // Since this extension doesn't contain any scaling logic, the point of these tests is to test
        // that DurableTaskTriggerMetrics are being properly deserialized into PerformanceHeartbeats.
        // DurableTask already contains tests for conversion/scaling logic past that.
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetScaleStatus_DeserializesMetrics()
        {
            PerformanceHeartbeat[] heartbeats;
            DurableTaskTriggerMetrics[] metrics;

            this.GetCorrespondingHeartbeatsAndMetrics(out heartbeats, out metrics);

            var context = new ScaleStatusContext<DurableTaskTriggerMetrics>
            {
                WorkerCount = 1,
                Metrics = metrics,
            };

            // MatchEquivalentHeartbeats will ensure that an exception is thrown if GetScaleStatus
            // tried to call MakeScaleRecommendation with an unexpected PerformanceHeartbeat[]
            this.performanceMonitor
                .Setup(m => m.MakeScaleRecommendation(1, this.MatchEquivalentHeartbeats(heartbeats)))
                .Returns<ScaleRecommendation>(null);

            var recommendation = this.scaleMonitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, recommendation.Vote);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetScaleStatus_HandlesMalformedMetrics()
        {
            // Null metrics
            var context = new ScaleStatusContext<DurableTaskTriggerMetrics>
            {
                WorkerCount = 1,
                Metrics = null,
            };

            var recommendation = this.scaleMonitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, recommendation.Vote);

            // Empty metrics
            var heartbeats = new PerformanceHeartbeat[0];
            context.Metrics = new DurableTaskTriggerMetrics[0];

            this.performanceMonitor
                .Setup(m => m.MakeScaleRecommendation(1, heartbeats))
                .Returns<ScaleRecommendation>(null);

            recommendation = this.scaleMonitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, recommendation.Vote);

            // Metrics with null properties
            var metrics = new DurableTaskTriggerMetrics[5];
            for (int i = 0; i < metrics.Length; ++i)
            {
                metrics[i] = new DurableTaskTriggerMetrics();
            }

            context.Metrics = metrics;

            heartbeats = new PerformanceHeartbeat[5];
            for (int i = 0; i < heartbeats.Length; ++i)
            {
                heartbeats[i] = new PerformanceHeartbeat
                {
                    ControlQueueLengths = new List<int>(),
                    ControlQueueLatencies = new List<TimeSpan>(),
                };
            }

            this.performanceMonitor
                .Setup(m => m.MakeScaleRecommendation(1, this.MatchEquivalentHeartbeats(heartbeats)))
                .Returns<ScaleRecommendation>(null);

            recommendation = this.scaleMonitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, recommendation.Vote);
        }

        private void GetCorrespondingHeartbeatsAndMetrics(out PerformanceHeartbeat[] heartbeats, out DurableTaskTriggerMetrics[] metrics)
        {
            heartbeats = new PerformanceHeartbeat[]
            {
                new PerformanceHeartbeat
                {
                    PartitionCount = 4,
                    ControlQueueLengths = new List<int> { 1, 2, 3, 4 },
                    ControlQueueLatencies = new List<TimeSpan> { TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(3), TimeSpan.FromMilliseconds(4), },
                    WorkItemQueueLength = 5,
                    WorkItemQueueLatency = TimeSpan.FromMilliseconds(6),
                },
                new PerformanceHeartbeat
                {
                    PartitionCount = 7,
                    ControlQueueLengths = new List<int> { 8, 9, 10, 11 },
                    ControlQueueLatencies = new List<TimeSpan> { TimeSpan.FromMilliseconds(12), TimeSpan.FromMilliseconds(13), TimeSpan.FromMilliseconds(14), TimeSpan.FromMilliseconds(15), },
                    WorkItemQueueLength = 16,
                    WorkItemQueueLatency = TimeSpan.FromMilliseconds(17),
                },
            };

            metrics = new DurableTaskTriggerMetrics[]
            {
                new DurableTaskTriggerMetrics
                {
                    PartitionCount = 4,
                    ControlQueueLengths = "[1,2,3,4]",
                    ControlQueueLatencies = "[\"00:00:00.0010000\",\"00:00:00.0020000\",\"00:00:00.0030000\",\"00:00:00.0040000\"]",
                    WorkItemQueueLength = 5,
                    WorkItemQueueLatency = "00:00:00.0060000",
                },
                new DurableTaskTriggerMetrics
                {
                    PartitionCount = 7,
                    ControlQueueLengths = "[8,9,10,11]",
                    ControlQueueLatencies = "[\"00:00:00.0120000\",\"00:00:00.0130000\",\"00:00:00.0140000\",\"00:00:00.0150000\"]",
                    WorkItemQueueLength = 16,
                    WorkItemQueueLatency = "00:00:00.0170000",
                },
            };
        }

        [Matcher]
        private PerformanceHeartbeat[] MatchEquivalentHeartbeats(PerformanceHeartbeat[] expected)
        {
            return Match.Create<PerformanceHeartbeat[]>(actual =>
            {
                if (expected.Length != actual.Length)
                {
                    return false;
                }

                bool[] matches = new bool[5];
                for (int i = 0; i < actual.Length; ++i)
                {
                    matches[0] = expected[i].PartitionCount == actual[i].PartitionCount;
                    matches[1] = expected[i].ControlQueueLatencies.SequenceEqual(actual[i].ControlQueueLatencies);
                    matches[2] = expected[i].ControlQueueLengths.SequenceEqual(actual[i].ControlQueueLengths);
                    matches[3] = expected[i].WorkItemQueueLength == actual[i].WorkItemQueueLength;
                    matches[4] = expected[i].WorkItemQueueLatency == actual[i].WorkItemQueueLatency;

                    if (matches.Any(m => m == false))
                    {
                        return false;
                    }
                }

                return true;
            });
        }
    }
}
