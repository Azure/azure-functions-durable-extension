// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Entities;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Context object passed to application code executing entity operations.
    /// </summary>
    internal class DurableEntityContext : IDurableEntityContext
    {
        private readonly EntityId self;
        private readonly TaskEntityShim shim;
        private readonly DurableTaskExtension config;
        private readonly MessagePayloadDataConverter messageDataConverter;
        private readonly MessagePayloadDataConverter errorDataConverter;

        public DurableEntityContext(DurableTaskExtension config, EntityId entity, TaskEntityShim shim)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.FunctionName = entity.EntityName;
            this.messageDataConverter = config.MessageDataConverter;
            this.errorDataConverter = config.ErrorDataConverter;
            this.self = entity;
            this.shim = shim;
        }

        // The last serialized checkpoint of the entity state is always stored in
        // this. LastSerializedState. The current state is determined by this.CurrentStateAccess and this.CurrentState.
        internal enum StateAccess
        {
            NotAccessed, // current state is stored in this.StartingState (serialized)
            Accessed, // current state is stored in this.CurrentState (deserialized)
            Clean, // current state is stored in both this.CurrentState (deserialized) and in this.State (serialized)
            Deleted, // current state is deleted
        }

        internal string LastSerializedState
        {
            get { return this.shim.BatchResult.EntityState; }
            set { this.shim.BatchResult.EntityState = value; }
        }

        private StateAccess CurrentStateAccess { get; set; }

        internal string FunctionName { get; }

        internal object CurrentState { get; set; }

        internal int CurrentOperationIndex { get; set; }

        internal OperationRequest CurrentOperation { get; set; }

        internal OperationResult CurrentOperationResult { get; set; }

        internal bool ExecutorCalledBack { get; set; }

        string IDurableEntityContext.EntityName => this.self.EntityName;

        string IDurableEntityContext.EntityKey => this.self.EntityKey;

        EntityId IDurableEntityContext.EntityId => this.self;

        int IDurableEntityContext.BatchPosition => this.CurrentOperationIndex;

        int IDurableEntityContext.BatchSize => this.shim.BatchRequest.Operations.Count;

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

                    default: return this.LastSerializedState != null;
                }
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

        public void AbortOnInternalError()
        {
            if (this.InternalError != null)
            {
                throw new SessionAbortedException($"Session aborted because of {this.InternalError.SourceException.GetType().Name}", this.InternalError.SourceException);
            }
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

        public void Rollback(int positionBeforeCurrentOperation)
        {
            // We discard the current state, which means we go back to the last serialized one
            this.CurrentStateAccess = StateAccess.NotAccessed;
            this.CurrentState = null;

            // we also roll back the list of outgoing messages,
            // so any signals sent by this operation are discarded.
            this.shim.BatchResult.Actions.RemoveRange(positionBeforeCurrentOperation, this.shim.BatchResult.Actions.Count - positionBeforeCurrentOperation);
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

            if (this.LastSerializedState != null && this.CurrentStateAccess != StateAccess.Deleted)
            {
                try
                {
                    result = this.messageDataConverter.Deserialize<TState>(this.LastSerializedState);
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

            if (this.LastSerializedState != null
                && this.CurrentStateAccess != StateAccess.Deleted)
            {
                try
                {
                    JsonConvert.PopulateObject(this.LastSerializedState, result, this.messageDataConverter.JsonSettings);
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

        internal bool TryWriteback(out OperationResult serializationErrorMessage, out Exception exception, string operationName = null, string operationId = null)
        {
            serializationErrorMessage = null;
            exception = null;
            bool entityExisted = this.LastSerializedState != null;

            if (this.CurrentStateAccess == StateAccess.Deleted)
            {
                this.LastSerializedState = null;
                this.CurrentStateAccess = StateAccess.NotAccessed;

                if (entityExisted)
                {
                    this.config.TraceHelper.EntityStateDeleted(
                        this.config.Options.HubName,
                        this.shim.Name,
                        this.shim.InstanceId,
                        operationName,
                        operationId,
                        isReplay: false);
                }
            }
            else if (this.CurrentStateAccess == StateAccess.Accessed)
            {
                string serializedState = null;
                Exception serializationException = null;
                try
                {
                    serializedState = this.messageDataConverter.Serialize(this.CurrentState);
                }
                catch (Exception e)
                {
                    serializationException = e;
                }

                if (serializationException == null)
                {
                    this.LastSerializedState = serializedState;
                    this.CurrentStateAccess = StateAccess.Clean;

                    if (!entityExisted)
                    {
                        this.config.TraceHelper.EntityStateCreated(
                            this.config.Options.HubName,
                            this.shim.Name,
                            this.shim.InstanceId,
                            operationName,
                            operationId,
                            isReplay: false);
                    }
                }
                else
                {
                    // we cannot serialize the entity state - this is an application error.
                    serializationException = new EntitySchedulerException(
                        $"Operation was rolled back because state for entity '{this.FunctionName}' could not be serialized: {serializationException.Message}", serializationException);

                    this.CaptureApplicationError(serializationException);

                    serializationErrorMessage = new OperationResult();
                    serializationErrorMessage.SetExceptionResult(serializationException, operationName, this.errorDataConverter);
                    exception = serializationException;

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
            this.config.ThrowIfFunctionDoesNotExist(functionName, FunctionType.Entity);

            var action = new SendSignalOperationAction()
            {
                InstanceId = EntityId.GetSchedulerIdFromEntityId(entity),
                Name = operation,
                ScheduledTime = scheduledTimeUtc,
                Input = null,
            };

            if (input != null)
            {
                action.Input = OperationInputExtensions.SerializeOperationInput(operation, input, this.messageDataConverter);
            }

            // add the action to the results, under a lock since user code may be concurrent
            lock (this.shim.BatchResult.Actions)
            {
                this.shim.BatchResult.Actions.Add(action);
            }

            this.config.TraceHelper.FunctionScheduled(
                this.config.Options.HubName,
                functionName,
                action.InstanceId,
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

            var action = new StartNewOrchestrationOperationAction()
            {
                InstanceId = instanceId,
                Name = functionName,
                Version = DurableOrchestrationContext.DefaultVersion,
                Tags = new Dictionary<string, string>() { { OrchestrationTags.FireAndForget, "" } },
            };

            if (input != null)
            {
                try
                {
                    if (input is JToken jtoken)
                    {
                        action.Input = jtoken.ToString(Formatting.None);
                    }
                    else
                    {
                        action.Input = this.messageDataConverter.Serialize(input);
                    }
                }
                catch (Exception e)
                {
                    throw new EntitySchedulerException($"Failed to serialize input for orchestration '{functionName}': {e.Message}", e);
                }
            }

            // add the action to the results, under a lock since user code may be concurrent
            lock (this.shim.BatchResult.Actions)
            {
                this.shim.BatchResult.Actions.Add(action);
            }

            this.config.TraceHelper.FunctionScheduled(
                this.config.Options.HubName,
                functionName,
                action.InstanceId,
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
            this.CurrentOperationResult.SetResult(result, this.messageDataConverter);
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
    }
}
