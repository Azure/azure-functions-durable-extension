// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Implements the entity scheduler as a looping orchestration.
    /// There is one such orchestration per entity.
    /// The orchestration terminates if the entity is deleted and idle.
    /// The orchestration calls ContinueAsNew when it is idle, but not deleted.
    /// </summary>
    internal class TaskEntityShim : TaskCommonShim
    {
        private readonly DurableEntityContext context;

        private readonly MessagePayloadDataConverter messageDataConverter;

        private readonly MessagePayloadDataConverter errorDataConverter;

        private readonly TaskCompletionSource<object> doneProcessingMessages
            = new TaskCompletionSource<object>();

        // a batch always consists of a (possibly empty) sequence of operations
        // followed by zero or one lock request, and possibly some operations to reschedule
        private readonly List<RequestMessage> operationBatch = new List<RequestMessage>();
        private RequestMessage lockRequest = null;
        private List<RequestMessage> toBeRescheduled;
        private bool suspendAndContinueWithDelay;

        private EntityTraceFlags entityTraceInfo;
        private int eventsReceived;

        public TaskEntityShim(DurableTaskExtension config, DurabilityProvider durabilityProvider, string schedulerId)
            : base(config)
        {
            this.messageDataConverter = config.MessageDataConverter;
            this.errorDataConverter = config.ErrorDataConverter;
            this.SchedulerId = schedulerId;
            this.EntityId = EntityId.GetEntityIdFromSchedulerId(schedulerId);
            this.context = new DurableEntityContext(config, durabilityProvider, this.EntityId, this);
        }

        public override DurableCommonContext Context => this.context;

        public string SchedulerId { get; private set; }

        public EntityId EntityId { get; private set; }

        public int NumberEventsToReceive { get; set; }

        internal List<RequestMessage> OperationBatch => this.operationBatch;

        internal RequestMessage LockRequest => this.lockRequest;

        internal int BatchPosition { get; private set; }

        public bool RollbackFailedOperations => this.context.Config.Options.RollbackEntityOperationsOnExceptions;

        public string TraceFlags => this.entityTraceInfo.TraceFlags;

        public void AddOperationToBatch(RequestMessage operationMessage)
        {
            this.operationBatch.Add(operationMessage);
        }

        public void AddLockRequestToBatch(RequestMessage lockRequest)
        {
            this.lockRequest = lockRequest;
        }

        public void AddMessageToBeRescheduled(RequestMessage requestMessage)
        {
            if (this.toBeRescheduled == null)
            {
                this.toBeRescheduled = new List<RequestMessage>();
            }

            this.toBeRescheduled.Add(requestMessage);
        }

        public void ToBeContinuedWithDelay()
        {
            if (!this.context.State.Suspended)
            {
                this.suspendAndContinueWithDelay = true;
            }
        }

        public override RegisteredFunctionInfo GetFunctionInfo()
        {
            FunctionName entityFunction = new FunctionName(this.Context.FunctionName);
            return this.Config.GetEntityInfo(entityFunction);
        }

        public override string GetStatus()
        {
            // We assemble a status object that compactly describes the current
            // state of the entity scheduler. It excludes all potentially large data
            // such as the entity state or the contents of the queue, so it always
            // has reasonable latency.

            EntityCurrentOperationStatus opStatus = null;
            if (this.context.CurrentOperation != null)
            {
                opStatus = new EntityCurrentOperationStatus()
                {
                    Operation = this.context.CurrentOperation.Operation,
                    Id = this.context.CurrentOperation.Id,
                    ParentInstanceId = this.context.CurrentOperation.ParentInstanceId,
                    StartTime = this.context.CurrentOperationStartTime,
                };
            }

            if (this.context.InternalError != null)
            {
                return $"Internal Error: {this.context.InternalError.SourceException}";
            }
            else
            {
                return this.messageDataConverter.Serialize(new EntityStatus()
                {
                    EntityExists = this.context.State.EntityExists,
                    QueueSize = this.context.State.Queue?.Count ?? 0,
                    LockedBy = this.context.State.LockedBy,
                    CurrentOperation = opStatus,
                });
            }
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            // no-op: the events were already processed outside of the DTFx context
            if (--this.NumberEventsToReceive == 0)
            {
                // signal the main orchestration thread that it can now safely terminate.
                this.doneProcessingMessages.SetResult(null);
            }
        }

        public void AddTraceFlag(char flag)
        {
            this.entityTraceInfo.AddFlag(flag);
        }

        internal void Rehydrate(string serializedInput)
        {
            if (serializedInput == null)
            {
                // this instance was automatically started by DTFx, or a previous execution called continueAsNew(null)
                this.context.State = new SchedulerState();
            }
            else
            {
                try
                {
                    // a previous incarnation of this instance called continueAsNew
                    this.context.State = this.messageDataConverter.Deserialize<SchedulerState>(serializedInput);
                }
                catch (Exception e)
                {
                    throw new EntitySchedulerException("Failed to deserialize entity scheduler state - may be corrupted or wrong version.", e);
                }
            }

            if (serializedInput == null)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    isReplay: false));
            }

            this.AddTraceFlag(EntityTraceFlags.Rehydrated);
        }

        // This code is executed via the middleware
        // see next() call DurableTaskExtension.EntityMiddleware.
        // At the time this is called, the batch has already executed, so this is simply called to
        // synchronize with the DurableTask.Core API.
        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            // Adding "Tags" to activity allows using App Insights to query current state of entities
            var activity = Activity.Current;
            OrchestrationRuntimeStatus status = OrchestrationRuntimeStatus.Running;

            DurableTaskExtension.TagActivityWithOrchestrationStatus(status, this.context.InstanceId, true);
            try
            {
                if (this.operationBatch.Count == 0
                    && this.lockRequest == null
                    && (this.toBeRescheduled == null || this.toBeRescheduled.Count == 0)
                    && !this.suspendAndContinueWithDelay)
                {
                    this.AddTraceFlag(EntityTraceFlags.WaitForEvents);

                    // we are idle after a ContinueAsNew - the batch is empty.
                    // Wait for more messages to get here (via extended sessions)
                    await this.doneProcessingMessages.Task;
                }

                if (!this.messageDataConverter.IsDefault)
                {
                    innerContext.MessageDataConverter = this.messageDataConverter;
                }

                if (!this.errorDataConverter.IsDefault)
                {
                    innerContext.ErrorDataConverter = this.errorDataConverter;
                }

                if (this.NumberEventsToReceive > 0)
                {
                    await this.doneProcessingMessages.Task;
                }

                // Commit the effects of this batch, if
                // we have not already run into an internal error
                // (in which case we will abort the batch instead of committing it)
                if (this.context.InternalError != null)
                {
                    this.Config.TraceHelper.EntityBatchFailed(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        this.entityTraceInfo.TraceFlags,
                        this.context.InternalError.SourceException);
                }
                else
                {
                    bool writeBackSuccessful = true;
                    ResponseMessage serializationErrorMessage = null;

                    if (this.RollbackFailedOperations)
                    {
                        // the state has already been written back, since it is
                        // done right after each operation.
                    }
                    else
                    {
                        // we are writing back the state here, after executing
                        // the entire batch of operations.
                        writeBackSuccessful = this.context.TryWriteback(out serializationErrorMessage, out var _);
                    }

                    // Reschedule all signals that were received before their time
                    this.context.RescheduleMessages(innerContext, this.toBeRescheduled);

                    // Send all buffered outgoing messages
                    this.context.SendOutbox(innerContext, writeBackSuccessful, serializationErrorMessage);

                    // send a continue signal
                    if (this.suspendAndContinueWithDelay)
                    {
                        this.AddTraceFlag(EntityTraceFlags.Suspended);
                        this.context.SendContinue(innerContext);
                        this.suspendAndContinueWithDelay = false;
                        this.context.State.Suspended = true;
                    }

                    if (this.Config.UseImplicitEntityDeletion && this.context.State.IsEmpty)
                    {
                        // this entity scheduler is idle and the entity is deleted, so the instance and history can be removed from storage
                        // we convey this to the durability provider by issuing a continue-as-new with null input
                        innerContext.ContinueAsNew(null);
                    }
                    else
                    {
                        // we persist the state of the entity scheduler and entity by issuing a continue-as-new
                        var jstate = JToken.FromObject(this.context.State);
                        innerContext.ContinueAsNew(jstate);
                    }

                    // trace the completion of this entity execution batch
                    this.Config.TraceHelper.EntityBatchCompleted(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        this.eventsReceived,
                        this.OperationBatch.Count,
                        this.BatchPosition,
                        this.context.State.MessageSorter.NumMessages,
                        this.context.State.Queue?.Count ?? 0,
                        this.context.State.UserStateSize,
                        this.context.State.MessageSorter.NumSources,
                        this.context.State.MessageSorter.NumDestinations,
                        this.context.State.LockedBy,
                        this.context.State.Suspended,
                        this.entityTraceInfo.TraceFlags);
                }
            }
            catch (Exception e)
            {
                // we must catch unexpected exceptions here, otherwise entity goes into permanent failed state
                // for example, there can be an OOM thrown during serialization https://github.com/Azure/azure-functions-durable-extension/issues/2166
                this.context.CaptureInternalError(e, this);
            }

            // The return value is not used.
            return string.Empty;
        }

        public async Task ExecuteBatch(CancellationToken onHostStopping)
        {
            async Task ExecuteOperationsInBatch()
            {
                if (this.GetFunctionInfo().IsOutOfProc)
                {
                    // process all operations in the batch using a single function call
                    await this.ExecuteOutOfProcBatch();
                }
                else
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // call the function once per operation in the batch
                    for (this.BatchPosition = 0; this.BatchPosition < this.operationBatch.Count; this.BatchPosition++)
                    {
                        // first, check if we should stop here and not execute the rest of the batch
                        bool InterruptBatchProcessing()
                        {
                            if (onHostStopping.IsCancellationRequested)
                            {
                                // host is shutting down
                                return true;
                            }
                            else if (this.TimeoutTask.IsCompleted)
                            {
                                // we timed out
                                this.AddTraceFlag(EntityTraceFlags.TimedOut);
                                return true;
                            }
                            else if (stopwatch.Elapsed > TimeSpan.FromMinutes(3))
                            {
                                // we have spent a significant time on this batch already
                                this.AddTraceFlag(EntityTraceFlags.SignificantTimeElapsed);
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }

                        if (InterruptBatchProcessing())
                        {
                            // put the requests back into the request queue
                            var queue = new Queue<RequestMessage>();
                            for (int i = this.BatchPosition; i < this.operationBatch.Count; i++)
                            {
                                queue.Enqueue(this.operationBatch[i]);
                            }

                            if (this.lockRequest != null)
                            {
                                queue.Enqueue(this.lockRequest);
                                this.lockRequest = null;
                            }

                            this.context.State.PutBack(queue);
                            this.ToBeContinuedWithDelay();
                            break;
                        }
                        else
                        {
                            // execute the operation
                            var request = this.operationBatch[this.BatchPosition];
                            await this.ProcessOperationRequestAsync(request);
                        }
                    }
                }
            }

            this.eventsReceived = this.NumberEventsToReceive;

            // process the operations, if any.
            if (this.operationBatch.Count > 0)
            {
                await ExecuteOperationsInBatch();
            }

            // process the lock request, if any. This is always the last operation in the batch.
            if (this.lockRequest != null)
            {
                this.ProcessLockRequest(this.lockRequest);
                this.lockRequest = null;
            }

            // if the host is shutting down, take note that we want to suspend processing
            if (onHostStopping.IsCancellationRequested)
            {
                this.AddTraceFlag(EntityTraceFlags.HostShutdown);
                this.ToBeContinuedWithDelay();
            }
        }

        public void ProcessLockRequest(RequestMessage request)
        {
            this.Config.TraceHelper.EntityLockAcquired(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                request.ParentInstanceId,
                request.ParentExecutionId,
                request.Id.ToString(),
                isReplay: false);

            // mark the entity state as locked
            this.context.State.LockedBy = request.ParentInstanceId;

            System.Diagnostics.Debug.Assert(request.LockSet[request.Position].Equals(this.EntityId), "position is correct");
            request.Position++;

            if (request.Position < request.LockSet.Length)
            {
                // send lock request to next entity in the lock set
                var target = new OrchestrationInstance() { InstanceId = EntityId.GetSchedulerIdFromEntityId(request.LockSet[request.Position]) };
                this.context.SendLockRequestMessage(target, request);
            }
            else
            {
                // send lock acquisition completed response back to originating orchestration instance
                var target = new OrchestrationInstance() { InstanceId = request.ParentInstanceId, ExecutionId = request.ParentExecutionId };
                this.context.SendLockResponseMessage(target, request.Id);
            }
        }

        private async Task ProcessOperationRequestAsync(RequestMessage request)
        {
            // set context for operation
            this.context.CurrentOperation = request;
            this.context.CurrentOperationResponse = new ResponseMessage();

            // set the async-local static context that is visible to the application code
            Entity.SetContext(this.context);

            bool operationFailed = false;
            var initialOutboxPosition = this.context.OutboxPosition;

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            Exception exception = null;

            var (callEntityActivity, processEntityInvocationActivity) = this.StartCallEntityAndEntityInvocationActivities(request);

            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task resultTask)
                {
                    var completedTask = await Task.WhenAny(resultTask, this.TimeoutTask);

                    if (completedTask == this.TimeoutTask)
                    {
                        exception = await this.TimeoutTask;
                    }
                    else
                    {
                        await resultTask;
                    }
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that is not awaitable!");
                }
            }
            catch (Exception e)
            {
                exception = e;
            }

            stopwatch.Stop();

            if (exception != null)
            {
                this.context.CaptureApplicationError(exception);

                // exception must be sent with response back to caller
                this.context.CurrentOperationResponse.SetExceptionResult(
                    exception,
                    this.context.CurrentOperation.Operation,
                    this.errorDataConverter);

                operationFailed = true;

                processEntityInvocationActivity?.SetTag(Schema.Task.ErrorMessage, exception.Message);
            }

            if (this.RollbackFailedOperations)
            {
                // we write back the entity state after each successful operation
                if (!operationFailed)
                {
                    if (!this.context.TryWriteback(out var errorResponseMessage, out exception, request.Operation, request.Id.ToString()))
                    {
                        // state serialization failed; create error response and roll back.
                        this.context.CurrentOperationResponse = errorResponseMessage;
                        operationFailed = true;
                    }
                }

                if (operationFailed)
                {
                    // discard changes and don't send any signals
                    this.context.Rollback(initialOutboxPosition);
                }
            }

            // clear the async-local static context that is visible to the application code
            Entity.SetContext(null);

            // read and clear context
            var response = this.context.CurrentOperationResponse;
            this.context.CurrentOperation = null;
            this.context.CurrentOperationResponse = null;

            if (!operationFailed)
            {
                this.Config.TraceHelper.OperationCompleted(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        request.Id.ToString(),
                        request.Operation,
                        this.context.RawInput,
                        response.Result,
                        stopwatch.Elapsed.TotalMilliseconds,
                        isReplay: false);
            }
            else
            {
                this.Config.TraceHelper.OperationFailed(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        request.Id.ToString(),
                        request.Operation,
                        this.context.RawInput,
                        exception,
                        stopwatch.Elapsed.TotalMilliseconds,
                        isReplay: false);
            }

            // send response
            if (!request.IsSignal)
            {
                var target = new OrchestrationInstance() { InstanceId = request.ParentInstanceId, ExecutionId = request.ParentExecutionId };
                var jresponse = JToken.FromObject(response, this.messageDataConverter.JsonSerializer);
                this.context.SendResponseMessage(target, request.Id, jresponse, response.IsException);
            }

            processEntityInvocationActivity?.Dispose();
            callEntityActivity?.Dispose();
        }

        private async Task ExecuteOutOfProcBatch()
        {
            OutOfProcResult outOfProcResult = null;

            Task invokeTask = await Task.WhenAny(this.FunctionInvocationCallback(), this.TimeoutTask);

            if (invokeTask == this.TimeoutTask)
            {
                var timeoutException = await this.TimeoutTask;

                // for now, we treat this as if ALL operations in the batch failed with a timeout exception.
                // in the future, we may want to catch timeouts out-of-proc and return results for the completed operations.
                outOfProcResult = new OutOfProcResult()
                {
                    EntityExists = this.context.State.EntityExists,
                    EntityState = this.context.State.EntityState,
                    Results = this.OperationBatch.Select(mbox => new OutOfProcResult.OperationResult()
                    {
                        IsError = true,
                        DurationInMilliseconds = 0,
                        Result = timeoutException.Message,
                    }).ToList(),
                    Signals = new List<OutOfProcResult.Signal>(),
                };
            }
            else if (invokeTask is Task<object> resultTask)
            {
                try
                {
                    var outOfProcResults = await resultTask;
                    var jObj = outOfProcResults as JObject;
                    outOfProcResult = jObj.ToObject<OutOfProcResult>();
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Out of proc orchestrators must return a valid JSON schema.", e);
                }
            }
            else
            {
                throw new InvalidOperationException("The WebJobs runtime returned a invocation task that does not support return values!");
            }

            // determine what happened to the entity state
            bool stateWasCreated = !this.context.State.EntityExists && outOfProcResult.EntityExists;
            bool stateWasDeleted = this.context.State.EntityExists && !outOfProcResult.EntityExists;

            // update the state
            this.context.State.EntityExists = outOfProcResult.EntityExists;
            this.context.State.EntityState = outOfProcResult.EntityState;

            // emit trace if state was created
            if (stateWasCreated)
            {
                this.Config.TraceHelper.EntityStateCreated(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        "", // operation name left blank because it is a batch
                        "", // operation id left blank because it is a batch
                        isReplay: false);
            }

            Dictionary<string, ActivityContext> parentTraceContexts = new ();

            // for each operation, emit trace and send response message (if not a signal)
            for (int i = 0; i < this.OperationBatch.Count; i++)
            {
                var request = this.OperationBatch[i];
                var result = outOfProcResult.Results[i];
                var startTime = DateTimeOffset.FromUnixTimeMilliseconds(result.ExecutionStartTimeInMilliseconds).UtcDateTime;

                var (callEntityActivity, processEntityInvocationActivity) = this.StartCallEntityAndEntityInvocationActivities(request, startTime);
                if (processEntityInvocationActivity != null)
                {
                    processEntityInvocationActivity.SetEndTime(startTime.AddMilliseconds(result.DurationInMilliseconds));
                    parentTraceContexts[request.Id.ToString()] = processEntityInvocationActivity.Context;
                }

                if (!result.IsError)
                {
                    this.Config.TraceHelper.OperationCompleted(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        request.Id.ToString(),
                        request.Operation,
                        request.Input,
                        result.Result,
                        result.DurationInMilliseconds,
                        isReplay: false);
                }
                else
                {
                    this.context.CaptureApplicationError(new OperationErrorException(
                        $"Error in operation '{request.Operation}': {result}"));

                    this.Config.TraceHelper.OperationFailed(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        request.Id.ToString(),
                        request.Operation,
                        request.Input,
                        result.Result,
                        result.DurationInMilliseconds,
                        isReplay: false);

                    processEntityInvocationActivity?.SetTag(Schema.Task.ErrorMessage, result.Result);
                }

                if (!request.IsSignal)
                {
                    var target = new OrchestrationInstance()
                    {
                        InstanceId = request.ParentInstanceId,
                        ExecutionId = request.ParentExecutionId,
                    };
                    var responseMessage = new ResponseMessage()
                    {
                        Result = result.Result,
                        ExceptionType = result.IsError ? "Error" : null,
                    };
                    this.context.SendResponseMessage(target, request.Id, responseMessage, !result.IsError);
                }

                processEntityInvocationActivity?.Dispose();
                callEntityActivity?.Dispose();
            }

            this.BatchPosition = this.OperationBatch.Count;

            // send signal messages
            foreach (var signal in outOfProcResult.Signals)
            {
                var request = new RequestMessage()
                {
                    ParentInstanceId = this.context.InstanceId,
                    ParentExecutionId = null, // for entities, message sorter persists across executions
                    Id = Guid.NewGuid(),
                    IsSignal = true,
                    Operation = signal.Name,
                    Input = signal.Input,
                };
                var target = new OrchestrationInstance()
                {
                    InstanceId = EntityId.GetSchedulerIdFromEntityId(signal.Target),
                };

                ActivityContext? parentTraceContext = null;
                if (!string.IsNullOrEmpty(signal.RequestId) && parentTraceContexts.ContainsKey(signal.RequestId))
                {
                    parentTraceContext = parentTraceContexts[signal.RequestId];
                }

                this.context.SendOperationMessage(target, request, parentTraceContext, DateTimeOffset.FromUnixTimeMilliseconds(signal.RequestTimeInMilliseconds));
            }

            if (stateWasDeleted)
            {
                this.Config.TraceHelper.EntityStateDeleted(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        "", // operation name left blank because it is a batch
                        "", // operation id left blank because it is a batch
                        isReplay: false);
            }
        }

        private (Activity, Activity) StartCallEntityAndEntityInvocationActivities(RequestMessage request, DateTimeOffset startTime = default)
        {
            Activity processEntityInvocationActivity = null;
            Activity callEntityActivity = null;

            // We only want to create a trace activity for processing the entity invocation in the case that we can successfully parse the trace context of the request that led to this entity invocation.
            // Otherwise, we will create an unlinked trace activity with no parent.
            if (ActivityContext.TryParse(request.ParentTraceContext?.TraceParent, request.ParentTraceContext?.TraceState, out ActivityContext parentTraceContext))
            {
                if (!request.IsSignal)
                {
                    callEntityActivity = TraceHelper.StartActivityForCallingOrSignalingEntity(
                        this.context.InstanceId,
                        this.context.Name,
                        request.Operation,
                        request.IsSignal,
                        request.ScheduledTime,
                        parentTraceContext,
                        request.RequestTime);
                    parentTraceContext = callEntityActivity.Context;
                }

                processEntityInvocationActivity = TraceHelper.StartActivityForProcessingEntityInvocation(
                        this.context.InstanceId,
                        this.context.Name,
                        request.Operation,
                        request.IsSignal,
                        parentTraceContext,
                        startTime);
            }

            return (callEntityActivity, processEntityInvocationActivity);
        }

        /// <summary>
        /// The results of executing a batch of operations on the entity out of process.
        /// </summary>
        internal class OutOfProcResult
        {
            /// <summary>
            /// Whether the entity exists after executing the batch.
            /// This is false if the last operation in the batch deletes the entity,
            /// and true otherwise.
            /// </summary>
            [JsonProperty("entityExists")]
            public bool EntityExists { get; set; }

            /// <summary>
            /// The state of the entity after executing the batch.
            /// Should be null if <see cref="EntityExists"/> is false.
            /// </summary>
            [JsonProperty("entityState")]
            public string EntityState { get; set; }

            /// <summary>
            /// The results of executing the operations. The length of this list must always match
            /// the size of the batch, even if there were exceptions.
            /// </summary>
            [JsonProperty("results")]
            public List<OperationResult> Results { get; set; }

            /// <summary>
            /// The list of signals sent by the entity. Can be empty.
            /// </summary>
            [JsonProperty("signals")]
            public List<Signal> Signals { get; set; }

            /// <summary>
            /// The results of executing an operation.
            /// </summary>
            public struct OperationResult
            {
                /// <summary>
                /// The returned value or error/exception.
                /// </summary>
                [JsonProperty("result")]
                public string Result { get; set; }

                /// <summary>
                /// Determines whether <see cref="Result"/> is a normal result, or an error/exception.
                /// </summary>
                [JsonProperty("isError")]
                public bool IsError { get; set; }

                /// <summary>
                /// The measured duration of this operation's execution, in milliseconds.
                /// </summary>
                [JsonProperty("duration")]
                public double DurationInMilliseconds { get; set; }

                /// <summary>
                /// The start time of this operation's execution, in milliseconds, since January 1st 1970 midnight in UTC.
                /// </summary>
                [JsonProperty("startTime")]
                public long ExecutionStartTimeInMilliseconds { get; set; }
            }

            /// <summary>
            /// Describes a signal that was emitted by one of the operations in the batch.
            /// </summary>
            public struct Signal
            {
                /// <summary>
                /// The destination of the signal.
                /// </summary>
                [JsonProperty("target")]
                public EntityId Target { get; set; }

                /// <summary>
                /// The name of the operation being signaled.
                /// </summary>
                [JsonProperty("name")]
                public string Name { get; set; }

                /// <summary>
                /// The input of the operation being signaled.
                /// </summary>
                [JsonProperty("input")]
                public string Input { get; set; }

                /// <summary>
                /// The ID of the request that triggered this signal.
                /// </summary>
                [JsonProperty("requestId")]
                public string RequestId { get; set; }

                /// <summary>
                /// The time the signal request was made, in milliseconds, since January 1st 1970 midnight in UTC.
                /// </summary>
                [JsonProperty("requestTime")]
                public long RequestTimeInMilliseconds { get; set; }
            }
        }
    }
}
