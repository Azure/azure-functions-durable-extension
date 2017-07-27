// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Task activity implementation which delegates the implementation to a function.
    /// </summary>
    internal class TaskActivityShim : TaskActivity
    {
        private readonly DurableTaskExtension config;
        private readonly ITriggeredFunctionExecutor executor;
        private readonly string activityName;
        private readonly string activityVersion;

        public TaskActivityShim(
            DurableTaskExtension config,
            ITriggeredFunctionExecutor executor,
            string activityName,
            string activityVersion)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            
            if (string.IsNullOrEmpty(activityName))
            {
                throw new ArgumentNullException(nameof(activityName));
            }

            this.activityName = activityName;
            this.activityVersion = activityVersion;
        }

        public override async Task<string> RunAsync(TaskContext context, string rawInput)
        {
            string instanceId = context.OrchestrationInstance.InstanceId;
            var inputContext = new DurableActivityContext(instanceId, rawInput);

            // TODO: Wire up the parent ID to improve dashboard logging.
            Guid? parentId = null;
            var triggerInput = new TriggeredFunctionData { ParentId = parentId, TriggerValue = inputContext };

            this.config.TraceHelper.FunctionStarting(
                this.config.HubName,
                this.activityName,
                this.activityVersion,
                instanceId,
                this.config.GetIntputOutputTrace(rawInput),
                isOrchestrator: false,
                isReplay: false);

            FunctionResult result = await this.executor.TryExecuteAsync(triggerInput, CancellationToken.None);
            if (!result.Succeeded)
            {
                this.config.TraceHelper.FunctionFailed(
                    this.config.HubName,
                    this.activityName,
                    this.activityVersion,
                    instanceId,
                    result.Exception?.ToString() ?? string.Empty,
                    isOrchestrator: false,
                    isReplay: false);

                if (result.Exception != null)
                {
                    // Preserve the original exception context so that the durable task
                    // framework can report useful failure information.
                    ExceptionDispatchInfo.Capture(result.Exception).Throw();
                }
            }

            string serializedOutput = inputContext.GetSerializedOutput();
            this.config.TraceHelper.FunctionCompleted(
                config.HubName,
                this.activityName,
                this.activityVersion,
                instanceId,
                this.config.GetIntputOutputTrace(serializedOutput),
                continuedAsNew: false,
                isOrchestrator: false,
                isReplay: false);

            return serializedOutput;
        }

        public override string Run(TaskContext context, string input)
        {
            // This won't get called as long as we've implemented RunAsync.
            throw new NotImplementedException();
        }
    }
}
