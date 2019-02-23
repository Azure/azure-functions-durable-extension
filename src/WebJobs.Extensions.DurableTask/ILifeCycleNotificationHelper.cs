// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to life cycle notifications.
    /// </summary>
    public interface ILifeCycleNotificationHelper
    {
        Task OrchestratorStartingAsync(string hubName, string functionName, string instanceId, FunctionType functionType, bool isReplay);

        Task OrchestratorCompletedAsync(string hubName, string functionName, string instanceId, bool continuedAsNew, FunctionType functionType, bool isReplay);

        Task OrchestratorFailedAsync(string hubName, string functionName, string instanceId, string reason, FunctionType functionType, bool isReplay);

        Task OrchestratorTerminatedAsync(string hubName, string functionName, string instanceId, string reason);
    }
}