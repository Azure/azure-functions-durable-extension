// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationClientMock : DurableOrchestrationClient
    {
        internal DurableOrchestrationClientMock(IOrchestrationServiceClient serviceClient, DurableTaskExtension config, OrchestrationClientAttribute attribute, EndToEndTraceHelper traceHelper) : base(serviceClient, config, attribute, traceHelper)
        {
        }

        public int Counter { get; set; }

        public override async Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId)
        {
            var runtimeStatus = OrchestrationRuntimeStatus.Running;
            switch (instanceId)
            {
                case TestConstants.IntanceIdFactComplete:
                    runtimeStatus = OrchestrationRuntimeStatus.Completed;
                    break;
                case TestConstants.InstanceIdIterations:
                    if (Counter < 3)
                    {
                        Counter++;
                    }
                    else
                    {
                        runtimeStatus = OrchestrationRuntimeStatus.Completed;
                    }
                    break;

                case TestConstants.InstanceIdFailed:
                    runtimeStatus = OrchestrationRuntimeStatus.Failed;
                    break;

                case TestConstants.InstanceIdTerminated:
                    runtimeStatus = OrchestrationRuntimeStatus.Terminated;
                    break;
                case TestConstants.InstanceIdCanceled:
                    runtimeStatus = OrchestrationRuntimeStatus.Canceled;
                    break;
            }
            return new DurableOrchestrationStatus
            {
                Name = "Sample Test",
                InstanceId = instanceId,
                CreatedTime = DateTime.Now,
                LastUpdatedTime = DateTime.Now,
                RuntimeStatus = runtimeStatus,
                Input = "",
                Output = "Hello Tokyo!"
            };
        }
    }
}
