// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
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

        private readonly MessagePayloadDataConverter messageDataConverter;

        private readonly MessagePayloadDataConverter errorDataConverter;

        private readonly DurabilityProvider durabilityProvider;

        private List<OutgoingMessage> outbox = new List<OutgoingMessage>();

        public DurableEntityContext(DurableTaskExtension config, DurabilityProvider durabilityProvider, EntityId entity, TaskEntityShim shim)
            : base(config, entity.EntityName)
        {
            this.messageDataConverter = config.MessageDataConverter;
            this.errorDataConverter = config.ErrorDataConverter;
            this.durabilityProvider = durabilityProvider;
            this.self = entity;
            this.shim = shim;
        }

        // The last serialized checkpoint of the entity state is always stored in
        // the fields this.State.EntityExists (a boolean) and this.State.EntityState (a string).
        // The current state is determined by this.CurrentStateAccess and this.CurrentState.
        internal enum StateAccess
        {
            NotAccessed, // current state is stored in this.State (serialized)
            Accessed, // current state is stored in this.CurrentState (deserialized)
            Clean, // current state is stored in both this.CurrentState (deserialized) and in this.State (serialized)
            Deleted, // current state is deleted
        }

        internal StateAccess CurrentStateAccess { get; set; }

        internal object CurrentState { get; set; }

        internal SchedulerState State { get; set; }

        internal RequestMessage CurrentOperation { get; set; }

        internal DateTime CurrentOperationStartTime { get; set; }

        internal ResponseMessage CurrentOperationResponse { get; set; }

        internal bool IsNewlyConstructed { get; set; }

        string IDurableEntityContext.EntityName => this.self.EntityName;

        string IDurableEntityContext.EntityKey => this.self.EntityKey;

        EntityId IDurableEntityContext.EntityId => this.self;

        int IDurableEntityContext.BatchPosition => this.shim.BatchPosition;

        int IDurableEntityContext.BatchSize => this.shim.OperationBatch.Count;

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
                switch (this.CurrentStateAccess)
                {
                    case StateAccess.Accessed:
                    case StateAccess.Clean:
                        return true;

                    case StateAccess.Deleted:
                        return false;

                    default: return this.State.EntityExists;
                }
            }
        }

        internal int OutboxPosition => this.outbox.Count;

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

        public void Rollback(int outboxPositionBeforeOperation)
        {
            // We discard the current state, which means we go back to the last serialized one
            this.CurrentStateAccess = StateAccess.NotAccessed;
            this.CurrentState = null;

            // we also roll back the list of outgoing messages,
            // so any signals sent by this operation are discarded.
            this.outbox.RemoveRange(outboxPositionBeforeOperation, this.outbox.Count - outboxPositionBeforeOperation);
        }

        void IDurableEntityContext.DeleteState()
        {
            this.ThrowIfInvalidAccess();

            this.CurrentStateAccess = StateAccess.Deleted;
            this.CurrentState = null;
        }

        TInput IDurableEntityContext.GetInput<TInput>()
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput<TInput>(this.messageDataConverter);
        }

        object IDurableEntityContext.GetInput(Type argumentType)
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput(argumentType, this.messageDataConverter);
        }

        TState IDurableEntityContext.GetState<TState>(Func<TState> initializer)
        {
            this.ThrowIfInvalidAccess();

            if (this.CurrentStateAccess == StateAccess.Accessed)
            {
                return (TState)this.CurrentState;
            }
            else if (this.CurrentStateAccess == StateAccess.Clean)
            {
                this.CurrentStateAccess = StateAccess.Accessed;
                return (TState)this.CurrentState;
            }

            TState result;

            if (this.State.EntityExists && this.CurrentStateAccess != StateAccess.Deleted)
            {
                try
                {
                    result = this.messageDataConverter.Deserialize<TState>(this.State.EntityState);
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

            this.CurrentStateAccess = StateAccess.Accessed;
            this.CurrentState = result;
            return result;
        }

        internal TState GetStateWithInjectedDependencies<TState>(Func<TState> constructor)
            where TState : class
        {
            this.ThrowIfInvalidAccess();

            if (this.CurrentStateAccess == StateAccess.Accessed)
            {
                return (TState)this.CurrentState;
            }
            else if (this.CurrentStateAccess == StateAccess.Clean)
            {
                this.CurrentStateAccess = StateAccess.Accessed;
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

            if (this.State.EntityExists
                && this.CurrentStateAccess != StateAccess.Deleted)
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

            this.CurrentStateAccess = StateAccess.Accessed;
            this.CurrentState = result;
            return result;
        }

        void IDurableEntityContext.SetState(object o)
        {
            this.ThrowIfInvalidAccess();

            this.CurrentState = o;
            this.CurrentStateAccess = StateAccess.Accessed;
        }

        internal bool TryWriteback(out ResponseMessage serializationErrorMessage, string operation = null)
        {
            serializationErrorMessage = null;

            if (this.CurrentStateAccess == StateAccess.Deleted)
            {
                this.State.EntityState = null;
                this.State.EntityExists = false;
                this.CurrentStateAccess = StateAccess.NotAccessed;
            }
            else if (this.CurrentStateAccess == StateAccess.Accessed)
            {
                try
                {
                    this.State.EntityState = this.messageDataConverter.Serialize(this.CurrentState);
                    this.State.EntityExists = true;
                    this.CurrentStateAccess = StateAccess.Clean;
                }
                catch (Exception e)
                {
                    // we cannot serialize the entity state - this is an application error.
                    var serializationException = new EntitySchedulerException(
                        $"Failed to serialize state of '{this.FunctionName}' entity: {e.Message}", e);

                    this.CaptureApplicationError(serializationException);

                    serializationErrorMessage = new ResponseMessage();
                    serializationErrorMessage.SetExceptionResult(serializationException, operation, this.errorDataConverter);

                    this.CurrentStateAccess = StateAccess.NotAccessed;
                    this.CurrentState = null;
                }
            }
            else
            {
                // the state was not accessed, or is clean, so we don't need to write anything back
            }

            return serializationErrorMessage == null;
        }

        void IDurableEntityContext.SignalEntity(EntityId entity, string operation, object input)
        {
            this.SignalEntityInternal(entity, null, operation, input);
        }

        void IDurableEntityContext.SignalEntity(EntityId entity, DateTime scheduledTimeUtc, string operation, object input)
        {
            this.SignalEntityInternal(entity, scheduledTimeUtc, operation, input);
        }

        private void SignalEntityInternal(EntityId entity, DateTime? scheduledTimeUtc, string operation, object input)
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
                ParentExecutionId = null, // for entities, message sorter persists across executions
                Id = Guid.NewGuid(),
                IsSignal = true,
                Operation = operation,
                ScheduledTime = scheduledTimeUtc,
            };
            if (input != null)
            {
                request.SetInput(input, this.messageDataConverter);
            }

            this.SendOperationMessage(target, request);

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

        async Task IDurableEntityContext.DispatchAsync<T>(params object[] constructorParameters)
        {
            IDurableEntityContext context = (IDurableEntityContext)this;
            MethodInfo method = FindMethodForContext<T>(context);

            if (method == null)
            {
                // We support a default delete operation even if the interface does not explicitly have a Delete method.
                if (string.Equals("delete", context.OperationName, StringComparison.InvariantCultureIgnoreCase))
                {
                    Entity.Current.DeleteState();
                    return;
                }
                else
                {
                    throw new InvalidOperationException($"No operation named '{context.OperationName}' was found.");
                }
            }

            // check that the number of arguments is zero or one
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length > 1)
            {
                throw new InvalidOperationException("Only a single argument can be used for operation input.");
            }

            object[] args;
            if (parameters.Length == 1)
            {
                // determine the expected type of the operation input and deserialize
                Type inputType = method.GetParameters()[0].ParameterType;
                object input = context.GetInput(inputType);
                args = new object[1] { input };
            }
            else
            {
                args = Array.Empty<object>();
            }

#if !FUNCTIONS_V1
            T Constructor() => (T)context.FunctionBindingContext.CreateObjectInstance(typeof(T), constructorParameters);
#else
            T Constructor() => (T)Activator.CreateInstance(typeof(T), constructorParameters);
#endif

            var state = ((Extensions.DurableTask.DurableEntityContext)context).GetStateWithInjectedDependencies(Constructor);

            object result = method.Invoke(state, args);

            if (method.ReturnType != typeof(void))
            {
                if (result is Task task)
                {
                    await task;

                    if (task.GetType().IsGenericType)
                    {
                        context.Return(task.GetType().GetProperty("Result").GetValue(task));
                    }
                }
                else
                {
                    context.Return(result);
                }
            }
        }

        void IDurableEntityContext.Return(object result)
        {
            this.ThrowIfInvalidAccess();
            this.CurrentOperationResponse.SetResult(result, this.messageDataConverter);
        }

        internal static MethodInfo FindMethodForContext<T>(IDurableEntityContext context)
        {
            var type = typeof(T);

            var interfaces = type.GetInterfaces();
            const BindingFlags bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var method = type.GetMethod(context.OperationName, bindingFlags);
            if (interfaces.Length == 0 || method != null)
            {
                return method;
            }

            return interfaces.Select(i => i.GetMethod(context.OperationName, bindingFlags)).FirstOrDefault(m => m != null);
        }

        private void ThrowIfInvalidAccess()
        {
            if (this.CurrentOperation == null)
            {
                throw new InvalidOperationException("No operation is being processed.");
            }
        }

        internal void SendOperationMessage(OrchestrationInstance target, RequestMessage requestMessage)
        {
            lock (this.outbox)
            {
                string eventName;

                if (requestMessage.ScheduledTime.HasValue)
                {
                    DateTime adjustedDeliveryTime = requestMessage.GetAdjustedDeliveryTime(this.durabilityProvider);
                    eventName = EntityMessageEventNames.ScheduledRequestMessageEventName(adjustedDeliveryTime);
                }
                else
                {
                    eventName = EntityMessageEventNames.RequestMessageEventName;
                }

                this.outbox.Add(new OperationMessage()
                {
                    Target = target,
                    EventName = eventName,
                    EventContent = requestMessage,
                });
            }
        }

        internal void SendResponseMessage(OrchestrationInstance target, Guid requestId, object message, bool isException)
        {
            lock (this.outbox)
            {
                this.outbox.Add(new ResultMessage()
                {
                    Target = target,
                    EventName = EntityMessageEventNames.ResponseMessageEventName(requestId),
                    EventContent = message,
                    IsError = isException,
                });
            }
        }

        internal void SendLockRequestMessage(OrchestrationInstance target, object message)
        {
            lock (this.outbox)
            {
                this.outbox.Add(new LockMessage()
                {
                    Target = target,
                    EventName = EntityMessageEventNames.RequestMessageEventName,
                    EventContent = message,
                });
            }
        }

        internal void SendLockResponseMessage(OrchestrationInstance target, Guid requestId)
        {
            lock (this.outbox)
            {
                this.outbox.Add(new LockMessage()
                {
                    Target = target,
                    EventName = EntityMessageEventNames.ResponseMessageEventName(requestId),
                    EventContent = new ResponseMessage()
                    {
                        Result = "Lock Acquisition Completed", // ignored by receiver but shows up in traces
                    },
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
                        // Since for all of the operations in the batch, their effect on the entity state
                        // is lost, we don't want the calling orchestrations to think everything is o.k.
                        // They should be notified, so we replace all non-error operation results
                        // with an exception result.
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
                        if (!operationMessage.EventContent.ScheduledTime.HasValue)
                        {
                            this.State.MessageSorter.LabelOutgoingMessage(operationMessage.EventContent, operationMessage.Target.InstanceId, DateTime.UtcNow, this.Config.MessageReorderWindow);
                        }

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

        internal void RescheduleMessages(OrchestrationContext innerContext, List<RequestMessage> messages)
        {
            if (messages != null)
            {
                foreach (var message in messages)
                {
                    var instance = new OrchestrationInstance { InstanceId = this.InstanceId };
                    DateTime adjustedDeliveryTime = message.GetAdjustedDeliveryTime(this.durabilityProvider);
                    var eventName = EntityMessageEventNames.ScheduledRequestMessageEventName(adjustedDeliveryTime);
                    innerContext.SendEvent(instance, eventName, message);
                }

                messages.Clear();
            }
        }

        /// <inheritdoc/>
        void IDurableEntityContext.SignalEntity<TEntityInterface>(string entityKey, Action<TEntityInterface> operation)
        {
            ((IDurableEntityContext)this).SignalEntity<TEntityInterface>(new EntityId(DurableEntityProxyHelpers.ResolveEntityName<TEntityInterface>(), entityKey), operation);
        }

        /// <inheritdoc/>
        void IDurableEntityContext.SignalEntity<TEntityInterface>(string entityKey, DateTime scheduledTimeUtc, Action<TEntityInterface> operation)
        {
            ((IDurableEntityContext)this).SignalEntity<TEntityInterface>(new EntityId(DurableEntityProxyHelpers.ResolveEntityName<TEntityInterface>(), entityKey), scheduledTimeUtc, operation);
        }

        /// <inheritdoc/>
        void IDurableEntityContext.SignalEntity<TEntityInterface>(EntityId entityId, Action<TEntityInterface> operation)
        {
            operation(EntityProxyFactory.Create<TEntityInterface>(new EntityContextProxy(this), entityId));
        }

        void IDurableEntityContext.SignalEntity<TEntityInterface>(EntityId entityId, DateTime scheduledTimeUtc, Action<TEntityInterface> operation)
        {
            operation(EntityProxyFactory.Create<TEntityInterface>(new EntityContextProxy(this, scheduledTimeUtc), entityId));
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

            public RequestMessage EventContent { get; set; }
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
