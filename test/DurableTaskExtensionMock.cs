// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Moq;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskExtensionMock : DurableTaskExtension
    {
        protected internal override DurableOrchestrationClientBase GetClient(OrchestrationClientAttribute attribute)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            return new DurableOrchestrationClientMock(orchestrationServiceClientMock.Object, this, null, null);
        }
    }
}
