// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Implements the actor scheduler as a looping orchestration.
    /// There is one such orchestration per actor.
    /// The orchestration terminates if the actor is deleted and idle.
    /// The orchestration calls ContinueAsNew when it is idle, but not deleted.
    /// </summary>
    internal class TaskActorShim : TaskCommonShim
    {
        // each actor starts a limited number of operations before calling ContinueAsNew,
        // to prevent the history from growing too large.
        // TODO explore alternate heuristics, make configurable and/or compute based on state size
        private const int OperationLimit = 20;

        private readonly DurableActorContext context;

        private readonly TaskCompletionSource<object> continueAsNewSignal
            = new TaskCompletionSource<object>();

        private Task lastStartedOperation = Task.CompletedTask;

        private int numberOperationsStarted = 0;

        public TaskActorShim(DurableTaskExtension config, string schedulerId)
            : base(config)
        {
            this.SchedulerId = schedulerId;
            this.ActorId = ActorId.GetActorIdFromSchedulerId(schedulerId);
            this.context = new DurableActorContext(config, this.ActorId);
        }

        public override DurableCommonContext Context => context;

        public string SchedulerId { get; private set; }

        public ActorId ActorId { get; private set; }

        public override RegisteredFunctionInfo GetFunctionInfo()
        {
            FunctionName actorFunction = new FunctionName(this.Context.FunctionName);
            return this.Config.GetActorInfo(actorFunction);
        }

        public override string GetStatus()
        {
            if (this.lastStartedOperation.IsCompleted)
            {
                // the actor is not currently processing an operation
                return "Available";
            }
            else
            {
                if (this.context.State.Queue != null)
                {
                    // the actor is currently processing an operation, and more operations are waiting in the queue)
                    return $"Busy ({this.context.State.Queue.Count} queued)";
                }
                else
                {
                    // the actor is currently processing an operation
                    return "Busy";
                }
            }
        }

        private void SignalContinueAsNew()
        {
            MethodInfo trySetResult = typeof(TaskCompletionSource<object>).GetMethod("TrySetResult");
            trySetResult.Invoke(this.continueAsNewSignal, new[] { (object)null });
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            this.Config.TraceHelper.ExternalEventRaised(
                this.Context.HubName,
                this.Context.Name,
                this.Context.InstanceId,
                eventName,
                this.Config.GetIntputOutputTrace(serializedEventData),
                this.Context.IsReplaying);

            if (eventName == "op")
            {
                var operationMessage = JsonConvert.DeserializeObject<OperationMessage>(serializedEventData);

                if (this.lastStartedOperation.IsCompleted && this.numberOperationsStarted < OperationLimit)
                {
                    this.numberOperationsStarted++;
                    this.lastStartedOperation = this.ProcessRequestAsync(operationMessage);
                }
                else
                {
                    this.context.State.Enqueue(operationMessage);
                }
            }
            else // it's a response
            {
                this.Context.RaiseEvent(eventName, serializedEventData);
            }
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            if (this.FunctionInvocationCallback == null)
            {
                throw new InvalidOperationException($"The {nameof(this.FunctionInvocationCallback)} has not been assigned!");
            }

            this.context.InnerContext = innerContext;

            if (this.GetFunctionInfo().IsOutOfProc)
            {
                throw new NotImplementedException("out-of-proc actor support is not implemented yet");
            }

            if (!innerContext.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.IsReplaying));
            }

            try
            {
                if (serializedInput == null)
                {
                    // this instance was automatically started by DTFx
                    this.context.State = new SchedulerState();
                }
                else
                {
                    // a previous incarnation of this instance called continueAsNew
                    this.context.State = JsonConvert.DeserializeObject<SchedulerState>(serializedInput, MessagePayloadDataConverter.MessageSettings);
                }

                // if there were messages in the queue left over, start processing them
                while (this.lastStartedOperation.IsCompleted && this.context.State.TryDequeue(out var request))
                {
                    this.lastStartedOperation = this.ProcessRequestAsync(request);
                }

                // wait for the continue as new signal, which restarts the history with a state snapshot
                await this.continueAsNewSignal.Task;

                // we must not call continue as new while an operation is still pending
                while (!this.lastStartedOperation.IsCompleted)
                {
                    await this.lastStartedOperation;
                }

                this.context.State.CurrentStateView?.WriteBack();
                var jstate = JToken.FromObject(this.context.State);
                this.context.InnerContext.ContinueAsNew(jstate);

                this.Config.TraceHelper.FunctionCompleted(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.State.ToString(),
                    true,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying);

                // currently not calling this as it may be too heavy for actors
                // if (!this.context.IsReplaying)
                // {
                //    this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                //        this.context.HubName,
                //        this.context.Name,
                //        this.context.InstanceId,
                //        true,
                //        this.context.IsReplaying));
                // }

                return "continueAsNew";
            }
            catch (Exception e)
            {
                // something went wrong in our code (application exceptions don't make it here)

                string exceptionDetails = e.ToString();

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
                    $"Actor scheduler for '{this.context.Name}' failed: {e.Message}",
                    Utils.SerializeCause(e, MessagePayloadDataConverter.ErrorConverter));

                this.context.OrchestrationException = ExceptionDispatchInfo.Capture(orchestrationException);

                throw orchestrationException;
            }
            finally
            {
                this.context.IsCompleted = true;
            }
        }

        private async Task ProcessRequestAsync(OperationMessage request)
        {
            // set context for operation
            this.context.CurrentOperation = request;
            this.context.CurrentOperationResponse = new ResponseMessage();
            this.context.IsNewlyConstructed = !this.context.State.ActorExists;
            this.context.State.ActorExists = true;
            this.context.DestructOnExit = false;

            this.Config.TraceHelper.FunctionStarting(
                                    this.context.HubName,
                                    this.context.Name,
                                    this.context.InstanceId,
                                    this.Config.GetIntputOutputTrace(request.Content),
                                    FunctionType.Actor,
                                    this.context.IsReplaying);
            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task resultTask)
                {
                    await resultTask;
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that is not awaitable!");
                }
            }
            catch (Exception e)
            {
                // exception must be sent with response back to caller
                this.context.CurrentOperationResponse.SetExceptionResult(e, this.context.CurrentOperation.Operation, this.ActorId);

                string exceptionDetails = e.ToString();

                this.Config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    exceptionDetails,
                    FunctionType.Actor,
                    this.context.IsReplaying);
            }

            // read and clear context
            var response = this.context.CurrentOperationResponse;
            var destructOnExit = this.context.DestructOnExit;
            this.context.CurrentOperation = null;
            this.context.CurrentOperationResponse = null;

            // send response
            // TODO think about how to handle exceptions in signals
            if (!request.IsSignal)
            {
                var target = new OrchestrationInstance() { InstanceId = request.ParentInstanceId };
                var guid = request.Id.ToString();
                var jresponse = JToken.FromObject(response, MessagePayloadDataConverter.DefaultSerializer);
                this.context.InnerContext.SendEvent(target, guid, jresponse);
            }

            // destruct the actor if the application code requested it
            if (destructOnExit)
            {
                this.context.State.ActorExists = false;
                this.context.State.CurrentStateView = null;
                this.context.State.ActorState = null;
            }

            // if we have reached the limit on the number of operations to process,
            // call ContinueAsNew. This helps to limit the history size.
            if (this.numberOperationsStarted >= OperationLimit)
            {
                this.SignalContinueAsNew();
            }
            else
            {
                // start next request waiting in the queue if there is one
                if (this.context.State.TryDequeue(out var operationMessage))
                {
                    this.numberOperationsStarted++;
                    this.lastStartedOperation = this.ProcessRequestAsync(operationMessage);
                }

                // if there are no requests waiting, and the actor no longer exists,
                // call ContinueAsNew. This removes the history.
                else if (!this.context.State.ActorExists)
                {
                    this.SignalContinueAsNew();
                }
            }
        }
    }
}
