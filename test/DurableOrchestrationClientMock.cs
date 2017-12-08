using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationClientMock : DurableOrchestrationClient
    {

        private const string IntanceIdFactComplete = "7b59154ae666471993659902ed0ba742";
        private const string InstanceIdIterations = "7b59154ae666471993659902ed0ba749";

        internal DurableOrchestrationClientMock(IOrchestrationServiceClient serviceClient, DurableTaskExtension config, OrchestrationClientAttribute attribute, EndToEndTraceHelper traceHelper) : base(serviceClient, config, attribute, traceHelper)
        {
        }

        public int Counter { get; set; }


        public override async Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId)
        {
            var runtimeStatus = OrchestrationRuntimeStatus.Running;
            if (instanceId == IntanceIdFactComplete)
                runtimeStatus = OrchestrationRuntimeStatus.Completed;
            else if (instanceId == InstanceIdIterations)
            {
                if (Counter < 3)
                {
                    Counter++;
                }
                else
                {
                    runtimeStatus = OrchestrationRuntimeStatus.Completed;
                }
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
