// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationContextBaseTests
    {
        private const string FunctionName = "sampleFunction";
        private readonly Task completedTask = Task.CompletedTask;
        private readonly int stateValueTen = 10;
        private readonly Task<object> taskFromTen = Task.FromResult<object>(10);
        private readonly int stateValueFive = 5;
        private readonly Task<int> intResultTask = Task.FromResult(5);

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void IsReplaying_returns_false()
        {
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock.Object.IsReplaying.Should().BeFalse();
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CallActivityAsync_is_calling_overload_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock.Setup(x => x.CallActivityAsync(FunctionName, null)).Returns(this.completedTask);
            var result = durableOrchestrationContextBaseMock.Object.CallActivityAsync(FunctionName, null);
            result.Should().Be(this.completedTask);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityWithRetryAsync_is_calling_overload_method()
        {
            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 5);
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock
                .Setup(x => x.CallActivityWithRetryAsync<object>(FunctionName, retryOptions, null))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallActivityWithRetryAsync(FunctionName, retryOptions, null);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorAsync_is_calling_overload_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync<object>(FunctionName, null))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync(FunctionName, null);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorAsync_with_instanceId_is_calling_overload_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            var instanceId = Guid.NewGuid().ToString();
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync(FunctionName, instanceId, null))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync(FunctionName, instanceId, null);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorAsync_typed_task_is_calling_overload_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync<int>(FunctionName, null, null)).Returns(this.intResultTask);
            var result = await durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync<int>(FunctionName, null);
            result.Should().Be(this.stateValueFive);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorWithRetryAsync_with_4_parameters_is_calling_overload_method()
        {
            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 5);
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock
                .Setup(x => x.CallSubOrchestratorWithRetryAsync<object>(FunctionName, retryOptions, null, null))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorWithRetryAsync(FunctionName, retryOptions, null);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorWithRetryAsync_with_5_parameters_is_calling_overload_method()
        {
            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 5);
            var instanceId = Guid.NewGuid().ToString();
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock
                .Setup(x => x.CallSubOrchestratorWithRetryAsync<object>(FunctionName, retryOptions, instanceId, null))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorWithRetryAsync(FunctionName, retryOptions, instanceId, null);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorWithRetryAsync_typed_task_is_calling_overload_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync<int>(FunctionName, null, null)).ReturnsAsync(this.stateValueFive);
            var result = await durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync<int>(FunctionName, null);
            result.Should().Be(this.stateValueFive);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CreateTimer_is_calling_overload_method()
        {
            var dateTime = DateTime.Now;
            var cancelToken = CancellationToken.None;
            var durableOrchestrationContextBaseMock = new Mock<DurableOrchestrationContextBase> { CallBase = true };
            durableOrchestrationContextBaseMock.Setup(x => x.CreateTimer<object>(dateTime, null, cancelToken)).ReturnsAsync(this.stateValueFive);
            var result = durableOrchestrationContextBaseMock.Object.CreateTimer(dateTime, cancelToken);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueFive);
        }
    }
}
