// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class DurableClientMock : DurableClient, IDurableOrchestrationClient
    {
        internal DurableClientMock(DurabilityProvider serviceClient, DurableTaskExtension config, DurableClientAttribute attribute)
            : base(serviceClient, config, config.HttpApiHandler, attribute)
        {
        }

        public int Counter { get; set; }

        Task<DurableOrchestrationStatus> IDurableOrchestrationClient.GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput)
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
