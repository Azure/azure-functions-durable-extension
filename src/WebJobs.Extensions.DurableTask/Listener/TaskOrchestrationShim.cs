// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Task orchestration implementation which delegates the orchestration implementation to a function.
    /// </summary>
    internal class TaskOrchestrationShim : TaskCommonShim
    {
        private readonly DurableOrchestrationContext context;
        private readonly OutOfProcOrchestrationShim outOfProcShim;
        private readonly DurableTaskExtension config;

        public TaskOrchestrationShim(DurableTaskExtension config, DurabilityProvider durabilityProvider, string name)
            : base(config)
        {
            this.config = config;
            this.context = new DurableOrchestrationContext(config, durabilityProvider, name);
            this.outOfProcShim = new OutOfProcOrchestrationShim(this.context);
        }

        public override DurableCommonContext Context => this.context;

        public override RegisteredFunctionInfo GetFunctionInfo()
        {
            FunctionName orchestratorFunction = new FunctionName(this.Context.FunctionName);
            return this.Config.GetOrchestratorInfo(orchestratorFunction);
        }

        public override string GetStatus()
        {
            return this.context.GetSerializedCustomStatus();
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            this.context.RaiseEvent(eventName, serializedEventData);
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            // Supress "Variable is assigned but its value is never used" in Functions V1
#pragma warning disable CS0219
            OrchestrationRuntimeStatus status; // for reporting the status of the orchestration on App Insights
#pragma warning restore CS0219

            if (this.FunctionInvocationCallback == null)
            {
                throw new InvalidOperationException($"The {nameof(this.FunctionInvocationCallback)} has not been assigned!");
            }

            if (!this.config.MessageDataConverter.IsDefault)
            {
                innerContext.MessageDataConverter = this.config.MessageDataConverter;
            }

            if (!this.config.ErrorDataConverter.IsDefault)
            {
                innerContext.ErrorDataConverter = this.config.ErrorDataConverter;
            }

            this.context.InnerContext = innerContext;
            this.context.RawInput = serializedInput;

            this.Config.TraceHelper.FunctionStarting(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                this.Config.GetIntputOutputTrace(serializedInput),
                FunctionType.Orchestrator,
                this.context.IsReplaying);
            status = OrchestrationRuntimeStatus.Running;
#if !FUNCTIONS_V1
            // On a replay, the orchestrator will either go into a 'Completed'
            // state or a 'Failed' state. We want to avoid tagging them as
            // 'Running' while replaying because this could result in
            // Application Insights reporting the wrong status.
            if (!innerContext.IsReplaying)
            {
                DurableTaskExtension.TagActivityWithOrchestrationStatus(status, this.context.InstanceId);
            }
#endif

            var orchestratorInfo = this.Config.GetOrchestratorInfo(new FunctionName(this.context.Name));

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.IsReplaying));
            }

            OrchestrationInvocationResult result = await this.InvokeFunctionAsync(orchestratorInfo);

            // In-process exceptions can be handled now, out-of-proc exceptions can only be handled
            // after replaying their actions.
            if (!orchestratorInfo.IsOutOfProc && result.WasInProcessExceptionThrown())
            {
                this.TraceAndSendExceptionNotification(result.Exception.ToString());
                var orchestrationException = new OrchestrationFailureException(
                    $"Orchestrator function '{this.context.Name}' failed: {result.Exception.Message}",
                    Utils.SerializeCause(result.Exception, innerContext.ErrorDataConverter));

                this.context.OrchestrationException =
                    ExceptionDispatchInfo.Capture(orchestrationException);

                throw orchestrationException;
            }

            if (result.ReturnValue != null)
            {
                if (orchestratorInfo.IsOutOfProc)
                {
                    try
                    {
                        await this.outOfProcShim.HandleDurableTaskReplay(result);
                    }
                    catch (OrchestrationFailureException ex)
                    {
                        this.TraceAndSendExceptionNotification(ex.Details);
                        this.context.OrchestrationException = ExceptionDispatchInfo.Capture(ex);
                        throw ex;
                    }
                }
            }

            // release any locks that were held by the orchestration
            // just in case the application code did not do so already
            this.context.ReleaseLocks();

            string serializedOutput = this.context.GetSerializedOutput();

            this.Config.TraceHelper.FunctionCompleted(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                this.Config.GetIntputOutputTrace(serializedOutput),
                this.context.ContinuedAsNew,
                FunctionType.Orchestrator,
                this.context.IsReplaying);
            status = OrchestrationRuntimeStatus.Completed;

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.ContinuedAsNew,
                    this.context.IsReplaying));
            }
#if !FUNCTIONS_V1
            DurableTaskExtension.TagActivityWithOrchestrationStatus(status, this.context.InstanceId);
#endif
            return serializedOutput;
        }

        private async Task<OrchestrationInvocationResult> InvokeFunctionAsync(RegisteredFunctionInfo orchestratorInfo)
        {
            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task<object> resultTask)
                {
                    // Orchestrator threads cannot perform async I/O, so block on such out-of-proc threads.
                    // Possible performance implications; may need revisiting.
                    return new OrchestrationInvocationResult()
                    {
                        ReturnValue = orchestratorInfo.IsOutOfProc ? resultTask.Result : await resultTask,
                    };
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that does not support return values!");
                }
            }
            catch (Exception e)
            {
                if (orchestratorInfo.IsOutOfProc
                    && OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(e.InnerException, out string returnValue)
                    && !string.IsNullOrEmpty(returnValue))
                {
                    return new OrchestrationInvocationResult()
                    {
                        ReturnValue = returnValue,
                        Exception = e,
                    };
                }

                return new OrchestrationInvocationResult
                {
                    Exception = e,
                };
            }
            finally
            {
                this.context.IsCompleted = true;
            }
        }

        private void TraceAndSendExceptionNotification(string exceptionDetails)
        {
            this.config.TraceHelper.FunctionFailed(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                exceptionDetails,
                FunctionType.Orchestrator,
                this.context.IsReplaying);

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(
                    () => this.config.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        exceptionDetails,
                        this.context.IsReplaying));
            }
        }

        internal class OrchestrationInvocationResult
        {
            public object ReturnValue { get; set; }

            public Exception Exception { get; set; }

            // If an in-process orchestration invocation encounter an exception,
            // it will not have a return value
            public bool WasInProcessExceptionThrown()
            {
                return this.ReturnValue == null && this.Exception != null;
            }
        }
    }
}
