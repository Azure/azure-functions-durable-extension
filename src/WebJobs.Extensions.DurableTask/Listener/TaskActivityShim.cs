// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Task activity implementation which delegates the implementation to a function.
    /// </summary>
    internal class TaskActivityShim : TaskActivity
    {
        private readonly DurableTaskExtensionBase config;
        private readonly ITriggeredFunctionExecutor executor;
        private readonly string activityName;

        public TaskActivityShim(
            DurableTaskExtensionBase config,
            ITriggeredFunctionExecutor executor,
            string activityName)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));

            if (string.IsNullOrEmpty(activityName))
            {
                throw new ArgumentNullException(nameof(activityName));
            }

            this.activityName = activityName;
        }

        public override async Task<string> RunAsync(TaskContext context, string rawInput)
        {
            string instanceId = context.OrchestrationInstance.InstanceId;
            var inputContext = new DurableActivityContext(instanceId, rawInput);

            // TODO: Wire up the parent ID to improve dashboard logging.
            Guid? parentId = null;
            var triggerInput = new TriggeredFunctionData { ParentId = parentId, TriggerValue = inputContext };

            this.config.TraceHelper.FunctionStarting(
                this.config.Options.HubName,
                this.activityName,
                instanceId,
                this.config.GetIntputOutputTrace(rawInput),
                functionType: FunctionType.Activity,
                isReplay: false);

            FunctionResult result = await this.executor.TryExecuteAsync(triggerInput, CancellationToken.None);
            if (!result.Succeeded)
            {
                // Flow the original activity function exception to the orchestration
                // without the outer FunctionInvocationException.
                Exception exceptionToReport = StripFunctionInvocationException(result.Exception);

                this.config.TraceHelper.FunctionFailed(
                    this.config.Options.HubName,
                    this.activityName,
                    instanceId,
                    exceptionToReport?.ToString() ?? string.Empty,
                    functionType: FunctionType.Activity,
                    isReplay: false);

                if (exceptionToReport != null)
                {
                    throw new TaskFailureException(
                        $"Activity function '{this.activityName}' failed: {exceptionToReport.Message}",
                        Utils.SerializeCause(exceptionToReport, MessagePayloadDataConverter.ErrorConverter));
                }
            }

            string serializedOutput = inputContext.GetSerializedOutput();
            this.config.TraceHelper.FunctionCompleted(
                this.config.Options.HubName,
                this.activityName,
                instanceId,
                this.config.GetIntputOutputTrace(serializedOutput),
                continuedAsNew: false,
                functionType: FunctionType.Activity,
                isReplay: false);

            return serializedOutput;
        }

        public override string Run(TaskContext context, string input)
        {
            // This won't get called as long as we've implemented RunAsync.
            throw new NotImplementedException();
        }

        private static Exception StripFunctionInvocationException(Exception e)
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
