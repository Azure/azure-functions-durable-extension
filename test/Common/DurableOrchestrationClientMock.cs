// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class DurableOrchestrationClientMock : DurableOrchestrationClient
    {
        internal DurableOrchestrationClientMock(IOrchestrationServiceClient serviceClient, DurableTaskExtension config, OrchestrationClientAttribute attribute)
            : base(serviceClient, config, attribute)
        {
        }

        public int Counter { get; set; }

        public override Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory = false, bool showHistoryOutput = false, bool showInput = true)
        {
            var runtimeStatus = OrchestrationRuntimeStatus.Running;
            switch (instanceId)
            {
                case TestConstants.IntanceIdFactComplete:
                    runtimeStatus = OrchestrationRuntimeStatus.Completed;
                    break;
                case TestConstants.InstanceIdIterations:
                    if (this.Counter < 3)
                    {
                        this.Counter++;
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

            return Task.FromResult(new DurableOrchestrationStatus
            {
                Name = "Sample Test",
                InstanceId = instanceId,
                CreatedTime = DateTime.Now,
                LastUpdatedTime = DateTime.Now,
                RuntimeStatus = runtimeStatus,
                Input = "",
                Output = "Hello Tokyo!",
            });
        }
    }
}
