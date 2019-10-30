// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Context object passed to application code executing entity operations.
    /// </summary>
    internal class DurableEntityContext : DurableCommonContext, IDurableEntityContext
    {
        private readonly EntityId self;

        private readonly TaskEntityShim shim;

        private List<OutgoingMessage> outbox = new List<OutgoingMessage>();

        public DurableEntityContext(DurableTaskExtension config, EntityId entity, TaskEntityShim shim)
            : base(config, entity.EntityName)
        {
            this.self = entity;
            this.shim = shim;
        }

        internal bool StateWasAccessed { get; set; }

        internal object CurrentState { get; set; }

        internal SchedulerState State { get; set; }

        internal RequestMessage CurrentOperation { get; set; }

        internal DateTime CurrentOperationStartTime { get; set; }

        internal ResponseMessage CurrentOperationResponse { get; set; }

        internal bool IsNewlyConstructed { get; set; }

        string IDurableEntityContext.EntityName => this.self.EntityName;

        string IDurableEntityContext.EntityKey => this.self.EntityKey;

        EntityId IDurableEntityContext.EntityId => this.self;

        internal List<RequestMessage> OperationBatch => this.shim.OperationBatch;

        internal ExceptionDispatchInfo InternalError { get; set; }

        internal List<ExceptionDispatchInfo> ApplicationErrors { get; set; }

        internal EntityId Self => this.self;

        string IDurableEntityContext.OperationName
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.CurrentOperation.Operation;
            }
        }

        bool IDurableEntityContext.HasState
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.State.EntityExists;
            }
        }

#if !FUNCTIONS_V1
        public FunctionBindingContext FunctionBindingContext { get; set; }
#endif

        public void CaptureInternalError(Exception e)
        {
            this.InternalError = ExceptionDispatchInfo.Capture(e);
        }

        public void CaptureApplicationError(Exception e)
        {
            if (this.ApplicationErrors == null)
            {
                this.ApplicationErrors = new List<ExceptionDispatchInfo>();
            }

            this.ApplicationErrors.Add(ExceptionDispatchInfo.Capture(e));
        }

        public void ThrowInternalExceptionIfAny()
        {
            if (this.InternalError != null)
            {
                this.InternalError.Throw();
            }
        }

        public void ThrowApplicationExceptionsIfAny()
        {
            if (this.ApplicationErrors != null)
            {
                if (this.ApplicationErrors.Count == 1)
                {
                    // throw single exceptions directly
                    this.ApplicationErrors[0].Throw();
                }
                else
                {
                    // aggregate multiple exceptions
                    throw new AggregateException(
                        "One or more operations failed.",
                        this.ApplicationErrors.Select(i => i.SourceException));
                }
            }
        }

        public bool ErrorsPresent(out string description)
        {
            if (this.InternalError != null)
            {
                description = $"Internal error: {this.InternalError.SourceException}";
                return true;
            }
            else if (this.ApplicationErrors != null)
            {
                var messages = this.ApplicationErrors.Select(i => $"({i.SourceException.Message})");
                description = $"One or more operations failed: {string.Concat(messages)}";
                return true;
            }
            else
            {
                description = string.Empty;
                return false;
            }
        }

        void IDurableEntityContext.DeleteState()
        {
            this.ThrowIfInvalidAccess();

            this.StateWasAccessed = false;
            this.CurrentState = null;
            this.State.EntityExists = false;
            this.State.EntityState = null;
        }

        TInput IDurableEntityContext.GetInput<TInput>()
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput<TInput>();
        }

        object IDurableEntityContext.GetInput(Type argumentType)
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput(argumentType);
        }

        TState IDurableEntityContext.GetState<TState>(Func<TState> initializer)
        {
            this.ThrowIfInvalidAccess();

            if (this.StateWasAccessed)
            {
                return (TState)this.CurrentState;
            }

            TState result;

            if (this.State.EntityExists)
            {
                try
                {
                    result = JsonConvert.DeserializeObject<TState>(this.State.EntityState);
                }
                catch (Exception e)
                {
                    throw new EntitySchedulerException($"Failed to deserialize entity state: {e.Message}", e);
                }
            }
            else
            {
                if (initializer != null)
                {
                    try
                    {
                        result = initializer();
                    }
                    catch (Exception e)
                    {
                        throw new EntitySchedulerException($"Failed to initialize entity state: {e.Message}", e);
                    }
                }
                else
                {
                    result = default(TState);
                }
            }

            this.CurrentState = result;
            this.StateWasAccessed = true;
            this.State.EntityExists = true;
            return result;
        }

        internal TState GetStateWithInjectedDependencies<TState>(Func<TState> constructor)
            where TState : class
        {
            this.ThrowIfInvalidAccess();

            if (this.StateWasAccessed)
            {
                return (TState)this.CurrentState;
            }

            TState result;

            try
            {
                result = constructor();
            }
            catch (Exception e)
            {
                throw new EntitySchedulerException($"Failed to construct entity state object: {e.Message}", e);
            }

            if (this.State.EntityExists)
            {
                try
                {
                    JsonConvert.PopulateObject(this.State.EntityState, result);
                }
                catch (Exception e)
                {
                    throw new EntitySchedulerException($"Failed to populate entity state from JSON: {e.Message}", e);
                }
            }

            this.CurrentState = result;
            this.StateWasAccessed = true;
            this.State.EntityExists = true;
            return result;
        }

        void IDurableEntityContext.SetState(object o)
        {
            this.ThrowIfInvalidAccess();

            this.CurrentState = o;
            this.StateWasAccessed = true;
            this.State.EntityExists = true;
        }

        internal bool TryWriteback(out ResponseMessage serializationErrorMessage)
        {
            serializationErrorMessage = null;

            if (this.StateWasAccessed)
            {
                try
                {
                    this.State.EntityState = MessagePayloadDataConverter.Default.Serialize(this.CurrentState);
                }
                catch (Exception e)
                {
                    // we cannot serialize the entity state - this is an application error.
                    var serializationException = new EntitySchedulerException(
                        $"Failed to serialize state of '{this.FunctionName}' entity: {e.Message}", e);

                    this.CaptureApplicationError(serializationException);

                    // Since for all of the operations in the batch, their effect on the entity state
                    // is lost, we don't want the calling orchestrations to think everything is o.k.
                    // They should be notified, so we replace all non-error operation results
                    // with an exception result.
                    serializationErrorMessage = new ResponseMessage()
                    {
                        ExceptionType = serializationException.GetType().AssemblyQualifiedName,
                        Result = MessagePayloadDataConverter.ErrorConverter.Serialize(serializationException),
                    };
                }

                this.CurrentState = null;
                this.StateWasAccessed = false;
            }

            return serializationErrorMessage == null;
        }

        void IDurableEntityContext.SignalEntity(EntityId entity, string operation, object input)
        {
            this.ThrowIfInvalidAccess();
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            string functionName = entity.EntityName;
            this.Config.ThrowIfFunctionDoesNotExist(functionName, FunctionType.Entity);

            var target = new OrchestrationInstance()
            {
                InstanceId = EntityId.GetSchedulerIdFromEntityId(entity),
            };
            var request = new RequestMessage()
            {
                ParentInstanceId = this.InstanceId,
                Id = Guid.NewGuid(),
                IsSignal = true,
                Operation = operation,
            };
            if (input != null)
            {
                request.SetInput(input);
            }

            this.SendOperationMessage(target, "op", request);

            this.Config.TraceHelper.FunctionScheduled(
                this.Config.Options.HubName,
                functionName,
                this.InstanceId,
                reason: this.FunctionName,
                functionType: FunctionType.Entity,
                isReplay: false);
        }

        string IDurableEntityContext.StartNewOrchestration(string functionName, object input, string instanceId)
        {
            this.ThrowIfInvalidAccess();

            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = Guid.NewGuid().ToString();
            }
            else if (instanceId.StartsWith("@"))
            {
                throw new ArgumentException(nameof(instanceId), "Orchestration instance ids must not start with @");
            }

            lock (this.outbox)
            {
                this.outbox.Add(new FireAndForgetMessage()
                {
                    InstanceId = instanceId,
                    FunctionName = functionName,
                    Input = input,
                });
            }

            this.Config.TraceHelper.FunctionScheduled(
                this.Config.Options.HubName,
                functionName,
                this.InstanceId,
                reason: this.FunctionName,
                functionType: FunctionType.Orchestrator,
                isReplay: false);

            return instanceId;
        }

        void IDurableEntityContext.Return(object result)
        {
            this.ThrowIfInvalidAccess();
            this.CurrentOperationResponse.SetResult(result);
        }

        private void ThrowIfInvalidAccess()
        {
            if (this.CurrentOperation == null)
            {
                throw new InvalidOperationException("No operation is being processed.");
            }
        }

        internal void SendOperationMessage(OrchestrationInstance target, string eventName, object message)
        {
            lock (this.outbox)
            {
                if (message is RequestMessage requestMessage)
                {
                    this.State.MessageSorter.LabelOutgoingMessage(requestMessage, target.InstanceId, DateTime.UtcNow, this.EntityMessageReorderWindow);
                }

                this.outbox.Add(new OperationMessage()
                {
                    Target = target,
                    EventName = eventName,
                    EventContent = message,
                });
            }
        }

        internal void SendResponseMessage(OrchestrationInstance target, string eventName, object message, bool isException)
        {
            lock (this.outbox)
            {
                if (message is RequestMessage requestMessage)
                {
                    this.State.MessageSorter.LabelOutgoingMessage(requestMessage, target.InstanceId, DateTime.UtcNow, this.EntityMessageReorderWindow);
                }

                this.outbox.Add(new ResultMessage()
                {
                    Target = target,
                    EventName = eventName,
                    EventContent = message,
                    IsError = isException,
                });
            }
        }

        internal void SendLockMessage(OrchestrationInstance target, string eventName, object message)
        {
            lock (this.outbox)
            {
                this.outbox.Add(new LockMessage()
                {
                    Target = target,
                    EventName = eventName,
                    EventContent = message,
                });
            }
        }

        internal void SendOutbox(OrchestrationContext innerContext, bool writeBackSuccessful, ResponseMessage serializationErrorMessage)
        {
            lock (this.outbox)
            {
                foreach (var message in this.outbox)
                {
                    if (message is LockMessage lockMessage)
                    {
                        this.Config.TraceHelper.SendingEntityMessage(
                            this.InstanceId,
                            this.ExecutionId,
                            lockMessage.Target.InstanceId,
                            lockMessage.EventName,
                            lockMessage.EventContent);

                        innerContext.SendEvent(lockMessage.Target, lockMessage.EventName, lockMessage.EventContent);
                    }
                    else if (message is ResultMessage resultMessage)
                    {
                        // non-error result messages are replaced with the writeback failed response
                        if (!writeBackSuccessful && !resultMessage.IsError)
                        {
                            resultMessage.EventContent = serializationErrorMessage;
                        }

                        this.Config.TraceHelper.SendingEntityMessage(
                            this.InstanceId,
                            this.ExecutionId,
                            resultMessage.Target.InstanceId,
                            resultMessage.EventName,
                            resultMessage.EventContent);

                        innerContext.SendEvent(resultMessage.Target, resultMessage.EventName, resultMessage.EventContent);
                    }
                    else if (!writeBackSuccessful)
                    {
                        // all other messages (signals and fire-and-forget) are suppressed if the writeback failed
                        // this helps to keep the observer pattern correct, for example.
                    }
                    else if (message is OperationMessage operationMessage)
                    {
                        this.Config.TraceHelper.SendingEntityMessage(
                            this.InstanceId,
                            this.ExecutionId,
                            operationMessage.Target.InstanceId,
                            operationMessage.EventName,
                            operationMessage.EventContent);

                        innerContext.SendEvent(operationMessage.Target, operationMessage.EventName, operationMessage.EventContent);
                    }
                    else if (message is FireAndForgetMessage fireAndForgetMessage)
                    {
                        var dummyTask = innerContext.CreateSubOrchestrationInstance<object>(
                          fireAndForgetMessage.FunctionName,
                          DurableOrchestrationContext.DefaultVersion,
                          fireAndForgetMessage.InstanceId,
                          fireAndForgetMessage.Input,
                          new Dictionary<string, string>() { { OrchestrationTags.FireAndForget, "" } });

                        System.Diagnostics.Debug.Assert(dummyTask.IsCompleted, "task should be fire-and-forget");
                    }
                }

                this.outbox.Clear();
            }
        }

        private abstract class OutgoingMessage
        {
        }

        private class FireAndForgetMessage : OutgoingMessage
        {
            public string InstanceId { get; set; }

            public string FunctionName { get; set; }

            public object Input { get; set; }
        }

        private class OperationMessage : OutgoingMessage
        {
            public OrchestrationInstance Target { get; set; }

            public string EventName { get; set; }

            public object EventContent { get; set; }
        }

        private class ResultMessage : OutgoingMessage
        {
            public OrchestrationInstance Target { get; set; }

            public string EventName { get; set; }

            public object EventContent { get; set; }

            public bool IsError { get; set; }
        }

        private class LockMessage : OutgoingMessage
        {
            public OrchestrationInstance Target { get; set; }

            public string EventName { get; set; }

            public object EventContent { get; set; }
        }
    }
}
