// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Task activity implementation which delegates the implementation to a function.
    /// </summary>
    internal class TaskActivityShim : TaskActivity
    {
        private readonly DurableTaskExtension config;
        private readonly ITriggeredFunctionExecutor executor;
        private readonly IApplicationLifetimeWrapper hostServiceLifetime;
        private readonly string activityName;

        /// <summary>
        /// The DTFx-generated, auto-incrementing ID that uniquely identifies this activity function execution.
        /// </summary>
        private int taskEventId = -1;

        public TaskActivityShim(
            DurableTaskExtension config,
            ITriggeredFunctionExecutor executor,
            IApplicationLifetimeWrapper hostServiceLifetime,
            string activityName)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.hostServiceLifetime = hostServiceLifetime ?? throw new ArgumentNullException(nameof(hostServiceLifetime));

            if (string.IsNullOrEmpty(activityName))
            {
                throw new ArgumentNullException(nameof(activityName));
            }

            this.activityName = activityName;
        }

        public override async Task<string> RunAsync(TaskContext context, string rawInput)
        {
            string instanceId = context.OrchestrationInstance.InstanceId;
            var inputContext = new DurableActivityContext(this.config, instanceId, rawInput, this.activityName);

            // TODO: Wire up the parent ID to improve dashboard logging.
            Guid? parentId = null;
            var triggerInput = new TriggeredFunctionData { ParentId = parentId, TriggerValue = inputContext };

            this.config.TraceHelper.FunctionStarting(
                this.config.Options.HubName,
                this.activityName,
                instanceId,
                rawInput,
                functionType: FunctionType.Activity,
                isReplay: false,
                taskEventId: this.taskEventId);

            WrappedFunctionResult result = await FunctionExecutionHelper.ExecuteActivityFunction(
                this.executor,
                triggerInput,
                this.hostServiceLifetime.OnStopping);

            switch (result.ExecutionStatus)
            {
                case WrappedFunctionResult.FunctionResultStatus.Success:
                    string serializedOutput = inputContext.GetSerializedOutput();
                    this.config.TraceHelper.FunctionCompleted(
                        this.config.Options.HubName,
                        this.activityName,
                        instanceId,
                        serializedOutput,
                        continuedAsNew: false,
                        functionType: FunctionType.Activity,
                        isReplay: false,
                        taskEventId: this.taskEventId);

                    return serializedOutput;
                case WrappedFunctionResult.FunctionResultStatus.FunctionsHostStoppingError:
                case WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError:
                    this.config.TraceHelper.FunctionAborted(
                        this.config.Options.HubName,
                        this.activityName,
                        instanceId,
                        $"An internal error occurred while attempting to execute this function. The execution will be aborted and retried. Details: {result.Exception}",
                        functionType: FunctionType.Activity);

                    // This will abort the execution and cause the message to go back onto the queue for re-processing
                    throw new SessionAbortedException(
                        $"An internal error occurred while attempting to execute '{this.activityName}'.", result.Exception);

                case WrappedFunctionResult.FunctionResultStatus.UserCodeError:
                case WrappedFunctionResult.FunctionResultStatus.FunctionTimeoutError:
                    // Flow the original activity function exception to the orchestration
                    // without the outer FunctionInvocationException.
                    Exception? exceptionToReport = StripFunctionInvocationException(result.Exception);

                    if (OutOfProcExceptionHelpers.TryGetExceptionWithFriendlyMessage(
                        exceptionToReport,
                        out Exception friendlyMessageException))
                    {
                        exceptionToReport = friendlyMessageException;
                    }

                    this.config.TraceHelper.FunctionFailed(
                        this.config.Options.HubName,
                        this.activityName,
                        instanceId,
                        exceptionToReport,
                        functionType: FunctionType.Activity,
                        isReplay: false,
                        taskEventId: this.taskEventId);

                    throw new TaskFailureException(
                            $"Activity function '{this.activityName}' failed: {exceptionToReport!.Message}",
                            Utils.SerializeCause(exceptionToReport, this.config.ErrorDataConverter));
                default:
                    // we throw a TaskFailureException to ensure deserialization is possible.
                    var innerException = new Exception($"{nameof(TaskActivityShim.RunAsync)} does not handle the function execution status {result.ExecutionStatus}.");
                    throw new TaskFailureException(
                            $"Activity function '{this.activityName}' failed: {innerException}",
                            Utils.SerializeCause(innerException, this.config.ErrorDataConverter));
            }
        }

        public override string Run(TaskContext context, string input)
        {
            // This won't get called as long as we've implemented RunAsync.
            throw new NotImplementedException();
        }

        internal void SetTaskEventId(int taskEventId)
        {
            // We don't have the DTFx task event ID at TaskActivityShim-creation time
            // so we have to set it here, before DTFx calls the RunAsync method.
            this.taskEventId = taskEventId;
        }

        private static Exception? StripFunctionInvocationException(Exception? e)
        {
            var infrastructureException = e as FunctionInvocationException;
            if (infrastructureException?.InnerException != null)
            {
                return infrastructureException.InnerException;
            }

            return e;
        }
    }
}
