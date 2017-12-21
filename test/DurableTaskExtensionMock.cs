// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
    }
}
