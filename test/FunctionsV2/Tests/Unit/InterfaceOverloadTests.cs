// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests to make sure that calls to interface methods with closely related overloads
    /// do not change as we add/tweak methods on the interfaces.
    ///
    /// TODO: Add more tests: https://github.com/Azure/azure-functions-durable-extension/issues/1500.
    /// </summary>
    public class InterfaceOverloadTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task IDurableOrchestrationClient_RaiseEventAsync_StringEventData()
        {
            var mockClient = new Mock<IDurableOrchestrationClient>();

            var client = mockClient.Object;

            string instanceId = "INSTANCE_ID";
            string eventName = "EVENT_NAME";
            string eventData = "EVENT_DATA";
            await client.RaiseEventAsync(instanceId, eventName, eventData);

            // There may be a better or more generalizable way of testing which interface method was called, but in the interest
            // of adding a bug fix for https://github.com/Azure/azure-functions-durable-extension/issues/1472 in a timely manner,
            // this will do.
            mockClient.Verify(c => c.RaiseEventAsync(instanceId, eventName, (object)eventData), Times.Once());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task IDurableOrchestrationClient_StartNewAsync_ValueTypeInput()
        {
            // Regression test for https://github.com/Azure/azure-functions-durable-extension/issues/1814.
            // The StartNewAsync<T>(string, T) interface overload keeps its 'where T : class' constraint (removing
            // it from the public interface would be a source-breaking change for implicit implementers). Value-type
            // support is instead provided by the unconstrained StartNewAsync<T> extension method, which delegates to
            // the three-argument StartNewAsync<T>(string, string, T) overload using an empty instance id. Passing a
            // value tuple here both compiles (guarding the value-type scenario) and, at runtime, must route through
            // the extension method to the three-argument overload.
            var mockClient = new Mock<IDurableOrchestrationClient>();

            var client = mockClient.Object;

            string functionName = "FUNCTION_NAME";
            (string, int) input = ("test", 1);
            await client.StartNewAsync(functionName, input);

            // The extension method forwards to the three-argument overload with an empty instance id.
            mockClient.Verify(c => c.StartNewAsync(functionName, string.Empty, input), Times.Once());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task IDurableOrchestrationClient_StartNewAsync_ReferenceTypeInput_UsesInterfaceOverload()
        {
            // Guards that reference-type inputs continue to bind to the two-argument interface overload rather than
            // the new StartNewAsync<T> extension method (instance methods take precedence over extension methods),
            // so adding the extension does not change existing behavior for reference-type inputs.
            var mockClient = new Mock<IDurableOrchestrationClient>();

            var client = mockClient.Object;

            string functionName = "FUNCTION_NAME";
            object input = new object();
            await client.StartNewAsync(functionName, input);

            // The two-argument interface overload should be selected directly (no instance id is supplied).
            mockClient.Verify(c => c.StartNewAsync(functionName, input), Times.Once());
        }
    }
}
