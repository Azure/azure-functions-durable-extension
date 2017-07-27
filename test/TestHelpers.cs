// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestHelpers
    {
        public static JobHost GetJobHost(string taskHub = "CommonTestHub")
        {
            var config = new JobHostConfiguration { HostId = "durable-task-host" };
            config.ConfigureDurableFunctionTypeLocator(typeof(TestOrchestrations), typeof(TestActivities));
            config.UseDurableTask(new DurableTaskExtension
            {
                HubName = taskHub.Replace("_", ""),
                TraceInputsAndOutputs = true
            });

            var host = new JobHost(config);
            return host;
        }
    }
}
