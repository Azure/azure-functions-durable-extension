// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using Moq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
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
