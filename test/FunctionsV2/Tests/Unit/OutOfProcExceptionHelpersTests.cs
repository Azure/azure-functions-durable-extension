// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core.Exceptions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.TaskOrchestrationShim;
using HostRpcException = Microsoft.Azure.WebJobs.Script.Workers.Rpc.RpcException;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OutOfProcExceptionHelpersTests
    {
        private const string StateMarker = "\n\n$OutOfProcData$:";

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, false, 1)]
        [InlineData(false, true, 1)]
        [InlineData(true, false, 1)]
        [InlineData(true, true, 1)]
        [InlineData(false, false, null)]
        [InlineData(true, false, 0)]
        public async Task NestedRpcError_ReplaysOnlyOuterFailureState(bool pascalCase, bool completionInError, int? schemaVersion)
        {
            var innerState = new JObject
            {
                ["isDone"] = completionInError,
                ["actions"] = new JArray
                {
                    new JArray
                    {
                        new JObject { ["actionType"] = "CallActivity", ["functionName"] = "InnerActivity" },
                    },
                },
                ["output"] = "not the actual output",
                ["error"] = completionInError ? null : "inner failure",
            };
            string originalError = CreateRpcException("inner failure" + StateMarker + innerState.ToString(Formatting.None)).Message;
            string stateJson = CreateFailureState(originalError, pascalCase, schemaVersion);
            Exception exception = CreateRpcException(originalError + StateMarker + stateJson);

            Assert.True(OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(exception, out string extracted));
            Assert.Equal(stateJson, extracted);
            Assert.True(OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(exception, out Exception friendly));
            Assert.Equal(originalError, friendly.Message);
            Assert.Same(exception, friendly.InnerException);

            var context = new Mock<IDurableOrchestrationContext>(MockBehavior.Strict);
            context.SetupGet(ctx => ctx.Name).Returns("OuterOrchestrator");
            context.Setup(ctx => ctx.SetCustomStatus(It.Is<object>(status => status.ToString() == originalError)));
            context.Setup(ctx => ctx.CallActivityAsync("OuterActivity", It.IsAny<object>())).Returns(Task.CompletedTask);
            var shim = new OutOfProcOrchestrationShim(context.Object);
            var result = new OrchestrationInvocationResult(extracted, exception);

            OrchestrationFailureException failure = await Assert.ThrowsAsync<OrchestrationFailureException>(
                () => shim.ScheduleDurableTaskEvents(result));

            Assert.Contains(originalError, failure.Message);
            context.Verify(ctx => ctx.CallActivityAsync("OuterActivity", It.IsAny<object>()), Times.Once);
            context.Verify(ctx => ctx.SetCustomStatus(It.IsAny<object>()), Times.Once);
            context.VerifyGet(ctx => ctx.Name, Times.Once);
            context.VerifyNoOtherCalls();
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("error\nStack: copied stack")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":true,\"actions\":[]}\nStack: copied stack")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":[],\"error\":\"inner failure\"}\nStack: copied stack")]
        public void WorkerStackRepeatingError_DoesNotChangeFailureState(string originalError)
        {
            string stateJson = CreateFailureState(originalError);
            Exception exception = CreateRpcException(
                originalError + StateMarker + stateJson,
                $"Error: {originalError}\n    at Orchestrator (run.js:1:1)");

            Assert.True(OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(exception, out string extracted));
            Assert.Equal(stateJson, extracted);
            Assert.True(OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(exception, out Exception friendly));
            Assert.Equal(originalError, friendly.Message);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void RpcLineEndings_PreserveAppendedFailureState(string newline)
        {
            const string originalError = "error\nStack: copied stack";
            string stateJson = CreateFailureState(originalError);
            var exception = new Exception(
                $"Result: Failure{newline}Exception: {originalError}{StateMarker}{stateJson}{newline}Stack: at Worker.Invoke()");

            Assert.True(OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(exception, out string extracted));
            Assert.Equal(stateJson, extracted.TrimEnd('\r'));
            Assert.True(OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(exception, out Exception friendly));
            Assert.Equal(" " + originalError, friendly.Message);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("ordinary error")]
        [InlineData("error {not JSON}")]
        [InlineData("{\"isDone\":true,\"actions\":[],\"output\":\"not state\"}")]
        [InlineData("error\nStack: copied stack")]
        [InlineData("error\nException: copied exception\nStack: copied stack")]
        [InlineData("error\n\n$OutOfProcData$:{not JSON}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":true,\"actions\":[]}\nStack: copied stack")]
        [InlineData("error\r\nStack: copied stack\r\nException: copied exception")]
        public void WrappedError_PreservesOriginalMessageAndAppendedState(string originalError)
        {
            string stateJson = CreateFailureState(originalError);
            Exception exception = CreateRpcException(originalError + StateMarker + stateJson);

            Assert.True(OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(exception, out string extracted));
            Assert.Equal(stateJson, extracted);
            Assert.True(OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(exception, out Exception friendly));
            Assert.Equal(originalError, friendly.Message);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("ordinary {error}")]
        [InlineData("{\"isDone\":true,\"actions\":[]}")]
        [InlineData("error\n\n$OutOfProcData$:{not JSON}")]
        [InlineData("error\n\n$OutOfProcData$:{}")]
        [InlineData("error\n\n$OutOfProcData$:[]")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":true,\"actions\":[],\"error\":\"failure\"}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":[]}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":[],\"error\":\"\"}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":\"false\",\"actions\":[],\"error\":\"failure\"}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":null,\"error\":\"failure\"}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":[{}],\"error\":\"failure\"}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":[[null]],\"error\":\"failure\"}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":[],\"error\":{}}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"IsDone\":true,\"actions\":[],\"error\":\"failure\"}")]
        [InlineData("error\n\n$OutOfProcData$:{\"isDone\":false,\"actions\":[],\"error\":\"failure\"} trailing text")]
        public void UnframedOrInvalidState_PreservesOriginalFailure(string originalError)
        {
            Exception exception = CreateRpcException(originalError);

            Assert.False(OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(exception, out string extracted));
            Assert.Null(extracted);
            Assert.True(OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(exception, out Exception friendly));
            Assert.Equal(originalError, friendly.Message);
            Assert.Same(exception, friendly.InnerException);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void InvalidFinalEnvelope_DoesNotFallBackToNestedState()
        {
            string originalError = "inner failure" + StateMarker + CreateFailureState("inner failure");
            string message = originalError + StateMarker + "{malformed outer state}";
            Exception exception = CreateRpcException(message);

            Assert.False(OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(exception, out string extracted));
            Assert.Null(extracted);
            Assert.True(OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(exception, out Exception friendly));
            Assert.Equal(message, friendly.Message);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not an RPC exception")]
        [InlineData("Result: Failure\nException: failure")]
        [InlineData("Result: Failure\nStack: stack\nException: failure")]
        public void InvalidRpcFraming_DoesNotThrowOrExtractState(string message)
        {
            Exception exception = message == null ? null : new Exception(message);

            Assert.False(OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(exception, out string extracted));
            Assert.Null(extracted);
            Assert.False(OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(exception, out Exception friendly));
            Assert.Null(friendly);
        }

        private static string CreateFailureState(string error, bool pascalCase = false, int? schemaVersion = 1)
        {
            var state = new JObject
            {
                [pascalCase ? "IsDone" : "isDone"] = false,
                [pascalCase ? "Actions" : "actions"] = new JArray
                {
                    new JArray
                    {
                        new JObject { ["actionType"] = "CallActivity", ["functionName"] = "OuterActivity" },
                    },
                },
                [pascalCase ? "Output" : "output"] = null,
                [pascalCase ? "CustomStatus" : "customStatus"] = error,
                [pascalCase ? "Error" : "error"] = error,
            };
            if (schemaVersion.HasValue)
            {
                state[pascalCase ? "SchemaVersion" : "schemaVersion"] = schemaVersion.Value;
            }

            return state.ToString(Formatting.None);
        }

        private static Exception CreateRpcException(string message, string stack = "at Worker.Invoke()\n at Caller.Run()")
        {
            return new HostRpcException("Failure", message, stack);
        }
    }
}
