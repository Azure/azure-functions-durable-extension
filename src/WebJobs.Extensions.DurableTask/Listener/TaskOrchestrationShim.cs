﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Task orchestration implementation which delegates the orchestration implementation to a function.
    /// </summary>
    internal class TaskOrchestrationShim : TaskOrchestration
    {
        private readonly DurableTaskExtension config;
        private readonly DurableOrchestrationContext context;

        private Func<Task> functionInvocationCallback;

        public TaskOrchestrationShim(
            DurableTaskExtension config,
            DurableOrchestrationContext context)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal DurableOrchestrationContext Context => this.context;

        public void SetFunctionInvocationCallback(Func<Task> callback)
        {
            if (this.functionInvocationCallback != null)
            {
                throw new InvalidOperationException($"{nameof(this.SetFunctionInvocationCallback)} must be called only once.");
            }

            this.functionInvocationCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            if (this.functionInvocationCallback == null)
            {
                throw new InvalidOperationException($"The {nameof(this.functionInvocationCallback)} has not been assigned!");
            }

            this.context.AssignToCurrentThread();
            this.context.SetInput(innerContext, serializedInput);

            this.config.TraceHelper.FunctionStarting(
                this.context.HubName,
                this.context.Name,
                this.context.Version,
                this.context.InstanceId,
                this.config.GetIntputOutputTrace(serializedInput),
                FunctionType.Orchestrator,
                this.context.IsReplaying);

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.Version,
                    this.context.InstanceId,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying));
            }

            object returnValue;
            try
            {
                Task invokeTask = this.functionInvocationCallback();
                if (invokeTask is Task<object> resultTask)
                {
                    returnValue = await resultTask;
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that does not support return values!");
                }
            }
            catch (Exception e)
            {
                string exceptionDetails = e.ToString();
                this.config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.Version,
                    this.context.InstanceId,
                    exceptionDetails,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying);

                if (!this.context.IsReplaying)
                {
                    this.context.AddDeferredTask(() => this.config.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.Version,
                        this.context.InstanceId,
                        exceptionDetails,
                        FunctionType.Orchestrator,
                        this.context.IsReplaying));
                }

                throw new OrchestrationFailureException(
                    $"Orchestrator function '{this.context.Name}' failed: {e.Message}",
                    Utils.SerializeCause(e, MessagePayloadDataConverter.Default));
            }
            finally
            {
                this.context.IsCompleted = true;
            }

            if (returnValue != null)
            {
                this.context.SetOutput(returnValue);
            }

            string serializedOutput = this.context.GetSerializedOutput();

            this.config.TraceHelper.FunctionCompleted(
                this.context.HubName,
                this.context.Name,
                this.context.Version,
                this.context.InstanceId,
                this.config.GetIntputOutputTrace(serializedOutput),
                this.context.ContinuedAsNew,
                FunctionType.Orchestrator,
                this.context.IsReplaying);
            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.Version,
                    this.context.InstanceId,
                    this.context.ContinuedAsNew,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying));
            }

            return serializedOutput;
        }

        public override string GetStatus()
        {
            return this.context.GetSerializedCustomStatus();
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            this.config.TraceHelper.ExternalEventRaised(
                this.context.HubName,
                this.context.Name,
                this.context.Version,
                this.context.InstanceId,
                eventName,
                this.config.GetIntputOutputTrace(serializedEventData),
                this.context.IsReplaying);

            this.context.RaiseEvent(eventName, serializedEventData);
        }
    }
}
