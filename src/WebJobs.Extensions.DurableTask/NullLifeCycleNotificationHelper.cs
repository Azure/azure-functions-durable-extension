// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class NullLifeCycleNotificationHelper : ILifeCycleNotificationHelper
    {
        public Task OrchestratorStartingAsync(string hubName, string functionName, string instanceId, bool isReplay)
        {
            return Task.CompletedTask;
        }

        public Task OrchestratorCompletedAsync(string hubName, string functionName, string instanceId, bool continuedAsNew, bool isReplay)
        {
            return Task.CompletedTask;
        }

        public Task OrchestratorFailedAsync(string hubName, string functionName, string instanceId, string reason, bool isReplay)
        {
            return Task.CompletedTask;
        }

        public Task OrchestratorTerminatedAsync(string hubName, string functionName, string instanceId, string reason)
        {
            return Task.CompletedTask;
        }
    }
}