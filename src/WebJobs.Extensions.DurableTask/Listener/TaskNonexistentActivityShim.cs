// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    /// <summary>
    /// Task activity implementation used when an activity cannot be executed because it is not an
    /// active, runnable function. This covers two cases:
    /// <list type="bullet">
    /// <item>the activity function does not exist (e.g. it was deleted or renamed), and</item>
    /// <item>the activity function is registered/indexed but has no active listener because it is
    /// disabled but still deployed (its <see cref="RegisteredFunctionInfo.Executor"/> is <c>null</c>).</item>
    /// </list>
    /// In both cases we surface a deterministic <see cref="TaskFailureException"/> from
    /// <see cref="Run"/> so the orchestration receives a catchable failure, rather than throwing
    /// during object construction (which DTFx treats as a transient work-item failure and retries
    /// forever — a poison loop). See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
    /// </summary>
    internal class TaskNonexistentActivityShim : TaskActivity
    {
        private readonly DurableTaskExtension config;
        private readonly string activityName;
        private readonly bool isDisabled;

        public TaskNonexistentActivityShim(
            DurableTaskExtension config,
            string activityName,
            bool isDisabled = false)
        {
            this.config = config;
            this.activityName = activityName;
            this.isDisabled = isDisabled;
        }

        public override string Run(TaskContext context, string input)
        {
            if (this.isDisabled)
            {
                // Emit a warning so operators can diagnose why an in-flight orchestration's activity
                // started failing after a deploy that disabled (but kept) the activity function.
                this.config.TraceHelper.ExtensionWarningEvent(
                    hubName: this.config.Options.HubName,
                    functionName: this.activityName,
                    instanceId: context.OrchestrationInstance?.InstanceId ?? string.Empty,
                    message: $"Activity function '{this.activityName}' was scheduled but is disabled. Failing the activity.");
            }

            string message = this.isDisabled
                ? $"Activity function '{this.activityName}' is disabled."
                : $"Activity function '{this.activityName}' does not exist.";
            Exception exceptionToReport = new FunctionFailedException(message);

            throw new TaskFailureException(
                $"Activity function '{this.activityName}' failed: {exceptionToReport.Message}",
                Utils.SerializeCause(exceptionToReport, this.config.ErrorDataConverter));
        }
    }
}
