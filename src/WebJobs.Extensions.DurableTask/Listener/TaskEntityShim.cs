// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DurableTask.Core;
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

        internal int BatchPosition { get; private set; }

        public bool RollbackFailedOperations => this.context.Config.Options.RollbackEntityOperationsOnExceptions;

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

        internal void Rehydrate(string serializedInput)
        {
            if (serializedInput == null)
            {
                // this instance was automatically started by DTFx
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
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
#if !FUNCTIONS_V1
            // Adding "Tags" to activity allows using App Insights to query current state of entities
            var activity = Activity.Current;
            OrchestrationRuntimeStatus status = OrchestrationRuntimeStatus.Running;

            DurableTaskExtension.TagActivityWithOrchestrationStatus(status, this.context.InstanceId, true);
#endif

            if (this.operationBatch.Count == 0
                && this.lockRequest == null
                && (this.toBeRescheduled == null || this.toBeRescheduled.Count == 0))
            {
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

            this.Config.TraceHelper.FunctionStarting(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                this.Config.GetIntputOutputTrace(serializedInput),
                FunctionType.Entity,
                isReplay: false);

            if (this.NumberEventsToReceive > 0)
            {
                await this.doneProcessingMessages.Task;
            }

            // Commit the effects of this batch, if
            // we have not already run into an internal error
            // (in which case we will abort the batch instead of committing it)
            if (this.context.InternalError == null)
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
                    writeBackSuccessful = this.context.TryWriteback(out serializationErrorMessage);
                }

                // Reschedule all signals that were received before their time
                this.context.RescheduleMessages(innerContext, this.toBeRescheduled);

                // Send all buffered outgoing messages
                this.context.SendOutbox(innerContext, writeBackSuccessful, serializationErrorMessage);

                var jstate = JToken.FromObject(this.context.State);

                // continue as new
                innerContext.ContinueAsNew(jstate);
            }

            if (this.context.ErrorsPresent(out var description))
            {
                this.Config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    description,
                    functionType: FunctionType.Entity,
                    isReplay: false);
            }
            else
            {
                this.Config.TraceHelper.FunctionCompleted(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.Config.GetIntputOutputTrace(this.context.State.EntityState),
                    continuedAsNew: true,
                    functionType: FunctionType.Entity,
                    isReplay: false);
            }

            // The return value is not used.
            return string.Empty;
        }

        public async Task ExecuteBatch()
        {
            if (this.GetFunctionInfo().IsOutOfProc)
            {
                if (this.operationBatch.Count > 0)
                {
                    // process all operations in the batch using a single function call
                    await this.ExecuteOutOfProcBatch();
                }
            }
            else
            {
                // call the function once per operation in the batch
                for (this.BatchPosition = 0; this.BatchPosition < this.operationBatch.Count; this.BatchPosition++)
                {
                    var request = this.operationBatch[this.BatchPosition];
                    await this.ProcessOperationRequestAsync(request);
                }
            }

            // process the lock request, if any
            if (this.lockRequest != null)
            {
                this.ProcessLockRequest(this.lockRequest);
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

            System.Diagnostics.Debug.Assert(this.context.State.LockedBy == request.ParentInstanceId, "Lock was set.");

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

            string exceptionDetails = string.Empty;

            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task resultTask)
                {
                    await resultTask;

                    stopwatch.Stop();
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that is not awaitable!");
                }
            }
            catch (Exception e)
            {
                stopwatch.Stop();

                exceptionDetails = e.ToString();

                this.context.CaptureApplicationError(e);

                // exception must be sent with response back to caller
                this.context.CurrentOperationResponse.SetExceptionResult(
                    e,
                    this.context.CurrentOperation.Operation,
                    this.errorDataConverter);

                operationFailed = true;
            }

            if (this.RollbackFailedOperations)
            {
                // we write back the entity state after each successful operation
                if (!operationFailed)
                {
                    if (!this.context.TryWriteback(out var errorResponseMessage))
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
                        this.Config.GetIntputOutputTrace(this.context.RawInput),
                        this.Config.GetIntputOutputTrace(response.Result),
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
                        this.Config.GetIntputOutputTrace(this.context.RawInput),
                        exceptionDetails,
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
        }

        private async Task ExecuteOutOfProcBatch()
        {
            object outOfProcResults = null;

            Task invokeTask = this.FunctionInvocationCallback();
            if (invokeTask is Task<object> resultTask)
            {
                outOfProcResults = await resultTask;
            }
            else
            {
                throw new InvalidOperationException("The WebJobs runtime returned a invocation task that does not support return values!");
            }

            var jObj = outOfProcResults as JObject;
            if (jObj == null)
            {
                throw new ArgumentException("Out of proc orchestrators must return a valid JSON schema.");
            }

            var outOfProcResult = jObj.ToObject<OutOfProcResult>();

            // update the state
            this.context.State.EntityExists = outOfProcResult.EntityExists;
            this.context.State.EntityState = outOfProcResult.EntityState;

            // for each operation, emit trace and send response message (if not a signal)
            for (int i = 0; i < this.OperationBatch.Count; i++)
            {
                var request = this.OperationBatch[i];
                var result = outOfProcResult.Results[i];

                if (!result.IsError)
                {
                    this.Config.TraceHelper.OperationCompleted(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        request.Id.ToString(),
                        request.Operation,
                        this.Config.GetIntputOutputTrace(request.Input),
                        this.Config.GetIntputOutputTrace(result.Result),
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
                        this.Config.GetIntputOutputTrace(request.Input),
                        this.Config.GetIntputOutputTrace(result.Result),
                        result.DurationInMilliseconds,
                        isReplay: false);
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
            }

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
                this.context.SendOperationMessage(target, request);
            }
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
            }
        }
    }
}
