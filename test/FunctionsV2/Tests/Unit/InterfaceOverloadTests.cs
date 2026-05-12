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
    }
}
