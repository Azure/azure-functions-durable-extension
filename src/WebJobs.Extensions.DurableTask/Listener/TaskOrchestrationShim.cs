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
                serializedInput,
                FunctionType.Orchestrator,
                this.context.IsReplaying);
            status = OrchestrationRuntimeStatus.Running;

            // On a replay, the orchestrator will either go into a 'Completed'
            // state or a 'Failed' state. We want to avoid tagging them as
            // 'Running' while replaying because this could result in
            // Application Insights reporting the wrong status.
            if (!innerContext.IsReplaying)
            {
                DurableTaskExtension.TagActivityWithOrchestrationStatus(status, this.context.InstanceId);
            }

            var orchestratorInfo = this.Config.GetOrchestratorInfo(new FunctionName(this.context.Name));

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.IsReplaying));
            }

            await this.InvokeUserCodeAndHandleResults(orchestratorInfo, innerContext);

            // release any locks that were held by the orchestration
            // just in case the application code did not do so already
            this.context.ReleaseLocks();

            string serializedOutput = this.context.GetSerializedOutput();

            this.Config.TraceHelper.FunctionCompleted(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                serializedOutput,
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

            DurableTaskExtension.TagActivityWithOrchestrationStatus(status, this.context.InstanceId);
            return serializedOutput;
        }

        // Responsible for invoking the function, handling the exception, set the output, and if
        // the function execution is out-of-process, handles the replay.
        private async Task InvokeUserCodeAndHandleResults(
            RegisteredFunctionInfo orchestratorInfo,
            OrchestrationContext innerContext)
        {
            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task<object> resultTask)
                {
                    // Orchestrator threads cannot perform async I/O, so block on such out-of-proc threads.
                    // Possible performance implications; may need revisiting.
                    object returnValue = orchestratorInfo.IsOutOfProc ? resultTask.Result : await resultTask;

                    // If an "illegal await" (awaiting a non DF API) is detected, we throw an exception.
                    // This exception will not transition the orchestrator to a Failed state.
                    // TODO: look to fail orchestrator in illegal awaits. This may require DTFx support.
                    this.context.ThrowIfInvalidAccess();

                    if (returnValue != null)
                    {
                        if (orchestratorInfo.IsOutOfProc)
                        {
                            await this.TraceAndReplay(returnValue);
                        }
                        else
                        {
                            this.context.SetOutput(returnValue);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that does not support return values!");
                }
            }
            catch (Exception e)
            {
                if (orchestratorInfo != null
                    && orchestratorInfo.IsOutOfProc
                    && OutOfProcExceptionHelpers.TryExtractOutOfProcStateJson(e.InnerException, out string returnValue)
                    && !string.IsNullOrEmpty(returnValue))
                {
                    try
                    {
                        await this.TraceAndReplay(returnValue, e);
                    }
                    catch (OrchestrationFailureException ex)
                    {
                        this.TraceAndSendExceptionNotification(ex);
                        this.context.OrchestrationException = ExceptionDispatchInfo.Capture(ex);
                        throw;
                    }
                }
                else
                {
                    this.TraceAndSendExceptionNotification(e);
                    var orchestrationException = new OrchestrationFailureException(
                        $"Orchestrator function '{this.context.Name}' failed: {e.Message}",
                        Utils.SerializeCause(e, innerContext.ErrorDataConverter));

                    this.context.OrchestrationException =
                        ExceptionDispatchInfo.Capture(orchestrationException);

                    DurableTaskExtension.TagActivityWithOrchestrationStatus(OrchestrationRuntimeStatus.Failed, this.context.InstanceId);

                    throw orchestrationException;
                }
            }
            finally
            {
                this.context.IsCompleted = true;
            }
        }

        private void TraceAndSendExceptionNotification(Exception exception)
        {
            string exceptionDetails = exception.Message;
            if (exception is OrchestrationFailureException orchestrationFailureException)
            {
                exceptionDetails = orchestrationFailureException.Details;
            }

            this.config.TraceHelper.FunctionFailed(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                exception: exception,
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

        private async Task TraceAndReplay(object result, Exception ex = null)
        {
            var invocationResult = new OrchestrationInvocationResult(result, ex);
            await this.outOfProcShim.HandleDurableTaskReplay(invocationResult);
        }

        internal class OrchestrationInvocationResult
        {
            public OrchestrationInvocationResult(object returnValue, Exception ex = null)
            {
                this.ReturnValue = returnValue;
                this.Exception = ex;

                (JObject resultJObject, string resultJSONString) = this.ParseOOProcResult(returnValue);
                this.Json = resultJObject;
                this.JsonString = resultJSONString;
            }

            public object ReturnValue { get; }

            public Exception Exception { get; }

            public JObject Json { get; }

            public string JsonString { get; }

            private (JObject, string) ParseOOProcResult(object result)
            {
                string jsonString;
                JObject json = result as JObject;
                if (json == null)
                {
                    if (result is string text)
                    {
                        try
                        {
                            jsonString = text;
                            json = JObject.Parse(text);
                        }
                        catch
                        {
                            throw new ArgumentException("Out of proc orchestrators must return a valid JSON schema");
                        }
                    }
                    else
                    {
                        throw new ArgumentException("The data returned by the out-of-process function execution was not valid json.");
                    }
                }
                else // result was a JObject all long, need to assign jsonString
                {
                    jsonString = json.ToString();
                }

                return (json, jsonString);
            }
        }
    }
}
