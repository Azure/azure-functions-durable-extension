// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskExtensionMock : DurableTaskExtension
    {
        protected internal override DurableOrchestrationClient GetClient(OrchestrationClientAttribute attribute)
        {

            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            return new DurableOrchestrationClientMock(orchestrationServiceClientMock.Object, this, null, null);
        }

        protected internal override HttpResponseMessage CreateCheckStatusResponse(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute)
        {
            switch (instanceId)
            {
                case TestConstants.InstanceIdDurableOrchestrationClientTests:
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(TestConstants.SampleData)
                    };
            }

            return null;
        }

        protected internal override async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            switch (instanceId)
            {
                case TestConstants.InstanceIdDurableOrchestrationClientTests:
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(TestConstants.SampleData)
                    };
            }

            return null;
        }


    }
}
