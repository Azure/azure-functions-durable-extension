using System;
using Moq;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskExtensionMock : DurableTaskExtension
    {
        internal override  DurableOrchestrationClient GetClient(OrchestrationClientAttribute attribute)
        {

            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            return new DurableOrchestrationClientMock(orchestrationServiceClientMock.Object, this, null, null);
        }
    }
}
