// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
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
        private readonly DurableActorContext context;

        private readonly TaskCompletionSource<object> doneProcessingMessages
            = new TaskCompletionSource<object>();

        private object lockable = new object();

        private Task lastStartedOperation = Task.CompletedTask;

        private int numberOperationsStarted = 0;

        private int numberMessagesToReceive;

        public TaskActorShim(DurableTaskExtension config, string schedulerId)
            : base(config)
        {
            this.SchedulerId = schedulerId;
            this.ActorId = ActorId.GetActorIdFromSchedulerId(schedulerId);
            this.context = new DurableActorContext(config, this.ActorId);
        }

        public override DurableCommonContext Context => this.context;

        public string SchedulerId { get; private set; }

        public ActorId ActorId { get; private set; }

        public override RegisteredFunctionInfo GetFunctionInfo()
        {
            FunctionName actorFunction = new FunctionName(this.Context.FunctionName);
            return this.Config.GetActorInfo(actorFunction);
        }

        public override string GetStatus()
        {
            // We assemble a status object that compactly describes the current
            // state of the actor scheduler. It excludes all potentially large data
            // such as the actor state or the contents of the queue, so it always
            // has reasonable latency.

            ActorCurrentOperationStatus opStatus = null;
            if (this.context.CurrentOperation != null)
            {
                opStatus = new ActorCurrentOperationStatus()
                {
                    Operation = this.context.CurrentOperation.Operation,
                    Id = this.context.CurrentOperation.Id,
                    ParentInstanceId = this.context.CurrentOperation.ParentInstanceId,
                    StartTime = this.context.CurrentOperationStartTime,
                };
            }

            return MessagePayloadDataConverter.Default.Serialize(new ActorStatus()
            {
                ActorExists = this.context.State.ActorExists,
                QueueSize = this.context.State.Queue?.Count ?? 0,
                LockedBy = this.context.State.LockedBy,
                CurrentOperation = opStatus,
            });
        }

        internal void ProcessMessage(string eventName, string serializedEventData)
        {
            this.numberMessagesToReceive++;

            if (eventName == "op")
            {
                var operationMessage = JsonConvert.DeserializeObject<RequestMessage>(serializedEventData);

                lock (this.lockable)
                {
                    // the operation gets processed if either
                    // - it was sent by the lock holder (the latter issues only one at a time, so no need to for queueing)
                    // - there is no lock holder, no current operation being processed, and we have not reached the limit
                    if ((this.context.State.LockedBy != null
                         && this.context.State.LockedBy == operationMessage.ParentInstanceId)
                      || (this.context.State.LockedBy == null
                          && this.lastStartedOperation.IsCompleted))
                    {
                        this.numberOperationsStarted++;
                        this.lastStartedOperation = this.ProcessRequestAsync(operationMessage);
                    }
                    else
                    {
                        this.Config.TraceHelper.ActorOperationQueued(
                            this.Context.HubName,
                            this.Context.Name,
                            this.Context.InstanceId,
                            operationMessage.Id.ToString(),
                            operationMessage.Operation ?? "LockRequest",
                            this.Context.IsReplaying);

                        this.context.State.Enqueue(operationMessage);
                    }
                }
            }
            else if (eventName == "release")
            {
                var message = JsonConvert.DeserializeObject<ReleaseMessage>(serializedEventData);

                if (this.context.State.LockedBy == message.ParentInstanceId)
                {
                    lock (this.lockable)
                    {
                        this.Config.TraceHelper.ActorLockReleased(
                            this.Context.HubName,
                            this.Context.Name,
                            this.Context.InstanceId,
                            message.ParentInstanceId,
                            message.LockRequestId,
                            this.Context.IsReplaying);

                        this.context.State.LockedBy = null;

                        if (this.lastStartedOperation.IsCompleted
                         && this.context.State.TryDequeue(out var operationMessage))
                        {
                            this.numberOperationsStarted++;
                            this.lastStartedOperation = this.ProcessRequestAsync(operationMessage);
                        }
                    }
                }
            }
            else // it's a response
            {
                this.Context.RaiseEvent(eventName, serializedEventData);
            }
        }

        internal async Task WaitForLastOperation()
        {
            Task waitFor;
            lock (this.lockable)
            {
                waitFor = this.lastStartedOperation;
            }

            while (true)
            {
                await waitFor;

                lock (this.lockable)
                {
                    if (waitFor == this.lastStartedOperation)
                    {
                        return;
                    }
                    else
                    {
                        waitFor = this.lastStartedOperation;
                    }
                }
            }
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            // no-op: the events were already processed outside of the DTFx context
            if (--this.numberMessagesToReceive == 0)
            {
                // signal the main orchestration thread that it can now safely terminate.
                this.doneProcessingMessages.SetResult(null);
            }
        }

        internal void Rehydrate(string serializedInput)
        {
            if (this.GetFunctionInfo().IsOutOfProc)
            {
                throw new NotImplementedException("out-of-proc actor support is not implemented yet");
            }

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

            if (!this.context.State.ActorExists)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.IsReplaying));
            }

            // restart the processing if needed
            lock (this.lockable)
            {
                if (this.context.State.LockedBy == null && this.context.State.TryDequeue(out var request))
                {
                    this.numberOperationsStarted++;
                    this.lastStartedOperation = this.ProcessRequestAsync(request);
                }
            }
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            // Wait until all the actor events have been processed.

            if (this.numberOperationsStarted == 0)
            {
                // we are idle after a ContinueAsNew - no messages yet to process.
                // Wait for more messages to get here (via extended sessions)
                await doneProcessingMessages.Task;
            }

            // Send all buffered outgoing messages (signals, responses, and fire-and-forget orchestrations)
            this.context.SendOutbox(innerContext);

            if (this.numberMessagesToReceive > 0)
            {
                await this.doneProcessingMessages.Task;
            }

            // write back the state
            this.context.CurrentStateView?.WriteBack();
            var jstate = JToken.FromObject(this.context.State);

            // continue as new
            innerContext.ContinueAsNew(jstate);

            // The return value is not used.
            return string.Empty;
        }

        private async Task ProcessRequestAsync(RequestMessage request)
        {
            if (request.IsLockMessage)
            {
                this.ProcessLockRequest(request);
            }
            else
            {
                await this.ProcessOperationRequestAsync(request);
            }
        }

        private void ProcessLockRequest(RequestMessage request)
        {
            this.Config.TraceHelper.ActorLockAcquired(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                request.ParentInstanceId,
                request.Id.ToString(),
                this.context.IsReplaying);

            System.Diagnostics.Debug.Assert(this.context.State.LockedBy == null, "Lock not held already.");
            this.context.State.LockedBy = request.ParentInstanceId;

            System.Diagnostics.Debug.Assert(request.LockSet[request.Position].Equals(this.ActorId), "position is correct");
            request.Position++;

            if (request.Position < request.LockSet.Length)
            {
                // send lock request to next actor in the lock set
                var target = new OrchestrationInstance() { InstanceId = ActorId.GetSchedulerIdFromActorId(request.LockSet[request.Position]) };
                this.Context.SendActorMessage(target, "op", request);
            }
            else
            {
                // send lock acquisition completed response back to originating orchestration instance
                var target = new OrchestrationInstance() { InstanceId = request.ParentInstanceId };
                var message = new ResponseMessage()
                {
                    Result = "Lock Acquisition Completed", // ignored by receiver but shows up in traces
                };
                this.Context.SendActorMessage(target, request.Id.ToString(), message);
            }
        }

        private async Task ProcessOperationRequestAsync(RequestMessage request)
        {
            // set context for operation
            this.context.CurrentOperation = request;
            this.context.CurrentOperationResponse = new ResponseMessage();
            this.context.IsNewlyConstructed = !this.context.State.ActorExists;
            this.context.State.ActorExists = true;
            this.context.DestructOnExit = false;
            this.context.IsCompleted = false;

            this.Config.TraceHelper.FunctionStarting(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                request.Id.ToString(),
                request.Operation,
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

                // the first exception is also handed over to the functions runtime
                if (this.context.OrchestrationException == null)
                {
                    var operationException = new OrchestrationFailureException(
                        $"Operation '{request.Operation}' on actor {this.ActorId} failed: {e.Message}",
                        Utils.SerializeCause(e, MessagePayloadDataConverter.ErrorConverter));
                    this.context.OrchestrationException = ExceptionDispatchInfo.Capture(operationException);
                }

                this.Config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    request.Id.ToString(),
                    request.Operation,
                    reason: e.ToString(),
                    functionType: FunctionType.Actor,
                    isReplay: this.context.IsReplaying);
            }

            // read and clear context
            var response = this.context.CurrentOperationResponse;
            var destructOnExit = this.context.DestructOnExit;
            this.context.CurrentOperation = null;
            this.context.CurrentOperationResponse = null;
            this.context.IsCompleted = true;

            // send response
            // TODO think about how to handle exceptions in signals
            if (!request.IsSignal)
            {
                var target = new OrchestrationInstance() { InstanceId = request.ParentInstanceId };
                var guid = request.Id.ToString();
                var jresponse = JToken.FromObject(response, MessagePayloadDataConverter.DefaultSerializer);
                this.Context.SendActorMessage(target, guid, jresponse);
            }

            // destruct the actor if the application code requested it
            if (destructOnExit)
            {
                this.context.State.ActorExists = false;
                this.context.State.ActorState = null;
                this.context.CurrentStateView = null;
            }

            // if there are requests waiting in the queue that we can process now, and
            // we have not yet reached the limit, do that now; else continue as new

            lock (this.lockable)
            {
                if (this.context.State.LockedBy == null
                    && this.context.State.TryDequeue(out var operationMessage))
                {
                    this.Config.TraceHelper.FunctionCompleted(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        request.Id.ToString(),
                        request.Operation,
                        this.Config.GetIntputOutputTrace(response.Result),
                        continuedAsNew: false,
                        functionType: FunctionType.Actor,
                        isReplay: this.context.IsReplaying);

                    this.numberOperationsStarted++;
                    this.lastStartedOperation = this.ProcessRequestAsync(operationMessage);
                }
                else
                {
                    this.Config.TraceHelper.FunctionCompleted(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        request.Id.ToString(),
                        request.Operation,
                        this.Config.GetIntputOutputTrace(response.Result),
                        continuedAsNew: true,
                        functionType: FunctionType.Actor,
                        isReplay: this.context.IsReplaying);
                }
            }
        }

        public override void TraceAwait()
        {
            // we only trace the awaits that are happening inside an operation,
            // not the awaits that come from the scheduler loop

            if (this.context.CurrentOperation != null)
            {
                this.Config.TraceHelper.FunctionAwaited(
                    this.context.HubName,
                    this.context.Name,
                    this.context.FunctionType,
                    this.context.InstanceId,
                    this.context.CurrentOperation?.Id.ToString() ?? string.Empty,
                    this.context.CurrentOperation?.Operation ?? string.Empty,
                    this.context.IsReplaying);
            }
        }
    }
}
