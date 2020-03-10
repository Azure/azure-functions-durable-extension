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
    public class DurableContextExtensionsTests
    {
        private const string FunctionName = "sampleFunction";
        private readonly int stateValueTen = 10;
        private readonly Task<object> taskFromTen = Task.FromResult<object>(10);
        private readonly int stateValueFive = 5;
        private readonly Task<int> intResultTask = Task.FromResult(5);
        private readonly object inputObject = (object)3;
        private readonly string operationName = "myop";
        private readonly EntityId entityId = new EntityId("a", "b");
        private readonly TimeSpan timeSpan = TimeSpan.FromMinutes(1);

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CallActivityAsync_is_calling_extension_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.CallActivityAsync<object>(FunctionName, this.inputObject)).Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallActivityAsync(FunctionName, this.inputObject);
            result.Should().Be(this.taskFromTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityWithRetryAsync_is_calling_extension_method()
        {
            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 5);
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock
                .Setup(x => x.CallActivityWithRetryAsync<object>(FunctionName, retryOptions, this.inputObject))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallActivityWithRetryAsync(FunctionName, retryOptions, this.inputObject);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorAsync_is_calling_extension_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync<object>(FunctionName, null, this.inputObject))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync(FunctionName, this.inputObject);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorAsync_with_instanceId_is_calling_extension_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { CallBase = true };
            var instanceId = Guid.NewGuid().ToString();
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync<object>(FunctionName, instanceId, this.inputObject))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync(FunctionName, instanceId, this.inputObject);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorAsync_typed_task_is_calling_extension_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync<int>(FunctionName, null, this.inputObject)).Returns(this.intResultTask);
            var result = await durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync<int>(FunctionName, this.inputObject);
            result.Should().Be(this.stateValueFive);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorWithRetryAsync_with_4_parameters_is_calling_extension_method()
        {
            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 5);
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock
                .Setup(x => x.CallSubOrchestratorWithRetryAsync<object>(FunctionName, retryOptions, null, this.inputObject))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorWithRetryAsync(FunctionName, retryOptions, this.inputObject);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorWithRetryAsync_with_5_parameters_is_calling_extension_method()
        {
            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 5);
            var instanceId = Guid.NewGuid().ToString();
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock
                .Setup(x => x.CallSubOrchestratorWithRetryAsync<object>(FunctionName, retryOptions, instanceId, this.inputObject))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallSubOrchestratorWithRetryAsync(FunctionName, retryOptions, instanceId, this.inputObject);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorWithRetryAsync_typed_task_is_calling_extension_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.CallSubOrchestratorAsync<int>(FunctionName, null, this.inputObject)).ReturnsAsync(this.stateValueFive);
            var result = await durableOrchestrationContextBaseMock.Object.CallSubOrchestratorAsync<int>(FunctionName, this.inputObject);
            result.Should().Be(this.stateValueFive);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForExternalEvent_is_calling_extension_method()
        {
            var dateTime = DateTime.Now;
            var cancelToken = CancellationToken.None;
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.WaitForExternalEvent<object>(this.operationName)).Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.WaitForExternalEvent(this.operationName);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForExternalEvent_with_timeout_is_calling_extension_method()
        {
            var dateTime = DateTime.Now;
            var cancelToken = CancellationToken.None;
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.WaitForExternalEvent<object>(this.operationName, this.timeSpan, CancellationToken.None)).Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.WaitForExternalEvent(this.operationName, this.timeSpan);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CreateTimer_is_calling_extension_method()
        {
            var dateTime = DateTime.Now;
            var cancelToken = CancellationToken.None;
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.CreateTimer<object>(dateTime, null, cancelToken)).ReturnsAsync(this.stateValueFive);
            var result = durableOrchestrationContextBaseMock.Object.CreateTimer(dateTime, cancelToken);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueFive);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_without_content_is_calling_extension_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.CallEntityAsync<object>(this.entityId, this.operationName, null))
                .Returns(this.taskFromTen);
            var result = durableOrchestrationContextBaseMock.Object.CallEntityAsync(this.entityId, this.operationName);
            var resultValue = await (Task<object>)result;
            resultValue.Should().Be(this.stateValueTen);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public Task CallEntityAsync_with_resulttype_without_content_is_calling_extension_method()
        {
            var durableOrchestrationContextBaseMock = new Mock<IDurableOrchestrationContext> { };
            durableOrchestrationContextBaseMock.Setup(x => x.CallEntityAsync<int>(this.entityId, this.operationName, null))
                .Returns(this.intResultTask);
            var result = durableOrchestrationContextBaseMock.Object.CallEntityAsync<int>(this.entityId, this.operationName);
            result.Should().Be(this.intResultTask);
            return result;
        }
    }
}
