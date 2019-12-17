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
        private readonly MessagePayloadDataConverter dataConverter;

        public TaskOrchestrationShim(DurableTaskExtension config, DurabilityProvider durabilityProvider, string name)
            : base(config)
        {
            this.dataConverter = config.DataConverter;
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
            if (this.FunctionInvocationCallback == null)
            {
                throw new InvalidOperationException($"The {nameof(this.FunctionInvocationCallback)} has not been assigned!");
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

            var orchestratorInfo = this.Config.GetOrchestratorInfo(new FunctionName(this.context.Name));

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.IsReplaying));
            }

            object returnValue;
            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task<object> resultTask)
                {
                    // Orchestrator threads cannot perform async I/O, so block on such out-of-proc threads.
                    // Possible performance implications; may need revisiting.
                    returnValue = orchestratorInfo.IsOutOfProc ? resultTask.Result : await resultTask;
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that does not support return values!");
                }
            }
            catch (Exception e)
            {
                string exceptionDetails = e.ToString();
                this.Config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    exceptionDetails,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying);

                if (!this.context.IsReplaying)
                {
                    this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        exceptionDetails,
                        this.context.IsReplaying));
                }

                var orchestrationException = new OrchestrationFailureException(
                    $"Orchestrator function '{this.context.Name}' failed: {e.Message}",
                    Utils.SerializeCause(e, this.dataConverter.ErrorConverter));

                this.context.OrchestrationException = ExceptionDispatchInfo.Capture(orchestrationException);

                throw orchestrationException;
            }
            finally
            {
                this.context.IsCompleted = true;
            }

            if (returnValue != null)
            {
                if (orchestratorInfo.IsOutOfProc)
                {
                    var jObj = returnValue as JObject;
                    if (jObj == null && returnValue is string jsonText)
                    {
                        jObj = JObject.Parse(jsonText);
                    }

                    if (jObj != null)
                    {
                        await this.outOfProcShim.HandleOutOfProcExecutionAsync(jObj);
                    }
                    else
                    {
                        throw new ArgumentException("Out of proc orchestrators must return a valid JSON schema.");
                    }
                }
                else
                {
                    this.context.SetOutput(returnValue);
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
            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.ContinuedAsNew,
                    this.context.IsReplaying));
            }

            return serializedOutput;
        }
    }
}
