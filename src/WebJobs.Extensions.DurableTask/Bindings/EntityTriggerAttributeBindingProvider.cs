// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EntityTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskExtension config;
        private readonly string connectionName;

        public EntityTriggerAttributeBindingProvider(
            DurableTaskExtension config,
            string connectionName)
        {
            this.config = config;
            this.connectionName = connectionName;
        }

        public Task<ITriggerBinding?> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ParameterInfo parameter = context.Parameter;
            EntityTriggerAttribute? trigger = parameter.GetCustomAttribute<EntityTriggerAttribute>(inherit: false);
            if (trigger == null)
            {
                return Task.FromResult<ITriggerBinding?>(null);
            }

            // Priority for getting the name is [EntityTrigger], [FunctionName], method name
            string name = trigger.EntityName;
            if (string.IsNullOrEmpty(name))
            {
                MemberInfo method = context.Parameter.Member;
                name = method.GetCustomAttribute<FunctionNameAttribute>()?.Name ?? method.Name;
            }

            // The entity class name defaults to the method name.
            var entityName = new FunctionName(name);
            this.config.RegisterEntity(entityName, null);
            var binding = new EntityTriggerBinding(this.config, parameter, entityName, this.connectionName);
            return Task.FromResult<ITriggerBinding?>(binding);
        }

        private class EntityTriggerBinding : ITriggerBinding
        {
            private static readonly IReadOnlyDictionary<string, object?> EmptyBindingData = new Dictionary<string, object?>(capacity: 0);

            private readonly DurableTaskExtension config;
            private readonly ParameterInfo parameterInfo;
            private readonly FunctionName entityName;
            private readonly string connectionName;

            public EntityTriggerBinding(
                DurableTaskExtension config,
                ParameterInfo parameterInfo,
                FunctionName entityName,
                string connectionName)
            {
                this.config = config;
                this.parameterInfo = parameterInfo;
                this.entityName = entityName;
                this.connectionName = connectionName;
                this.BindingDataContract = GetBindingDataContract(parameterInfo);
            }

            // Out-of-proc V2 uses a different trigger value type
            public Type TriggerValueType => this.config.OutOfProcProtocol == OutOfProcOrchestrationProtocol.MiddlewarePassthrough ?
                typeof(RemoteEntityContext) :
                typeof(IDurableEntityContext);

            public IReadOnlyDictionary<string, Type> BindingDataContract { get; }

            private static IReadOnlyDictionary<string, Type> GetBindingDataContract(ParameterInfo parameterInfo)
            {
                var contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
                {
                    // This binding supports return values of any type
                    { "$return", typeof(object).MakeByRefType() },
                };

                // allow binding to the parameter name
                contract[parameterInfo.Name!] = parameterInfo.ParameterType;

                return contract;
            }

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                if (value is DurableEntityContext entityContext)
                {
                    Type destinationType = this.parameterInfo.ParameterType;

                    object? convertedValue = null;
                    if (destinationType == typeof(IDurableEntityContext))
                    {
                        convertedValue = entityContext;
                        ((IDurableEntityContext)value).FunctionBindingContext = context.FunctionContext;
                }
                else if (destinationType == typeof(string))
                {
                    convertedValue = EntityContextToString(entityContext);
                }

                    var inputValueProvider = new ObjectValueProvider(
                        convertedValue ?? value,
                        this.parameterInfo.ParameterType);

                    var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    bindingData[this.parameterInfo.Name!] = convertedValue;

                    var triggerData = new TriggerData(inputValueProvider, bindingData);
                    return Task.FromResult<ITriggerData>(triggerData);
                }
                else if (value is RemoteEntityContext remoteContext)
                {
                    // Generate a byte array which is the serialized protobuf payload
                    // https://developers.google.com/protocol-buffers/docs/csharptutorial#parsing_and_serialization
                    var entityBatchRequest = remoteContext.Request.ToEntityBatchRequest();

                    // We convert the binary payload into a base64 string because that seems to be the most commonly supported
                    // format for Azure Functions language workers. Attempts to send unencoded byte[] payloads were unsuccessful.
                    string encodedRequest = ProtobufUtils.Base64Encode(entityBatchRequest);
                    var contextValueProvider = new ObjectValueProvider(encodedRequest, typeof(string));
                    var triggerData = new TriggerData(contextValueProvider, EmptyBindingData);
                    return Task.FromResult<ITriggerData>(triggerData);
                }
                else
                {
                    throw new ArgumentException($"Don't know how to bind to {value?.GetType().Name ?? "null"}.", nameof(value));
                }
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor { Name = this.parameterInfo.Name };
            }

            public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                // The current assumption is that in-proc (.NET) apps always use
                // IEntityContext or some derivative. Non-.NET apps
                // which cannot use these types are therefore assumed to be "out-of-proc".
                // We may need to revisit this assumption when Functions v2 adds support
                // for "out-of-proc" .NET.
                var isOutOfProc = !typeof(IDurableEntityContext).IsAssignableFrom(this.parameterInfo.ParameterType);
                this.config.RegisterEntity(this.entityName, new RegisteredFunctionInfo(context.Executor, isOutOfProc));

                var listener = new DurableTaskListener(
                    this.config,
                    context.Descriptor.Id,
                    this.entityName,
                    FunctionType.Entity,
                    this.connectionName);
                return Task.FromResult<IListener>(listener);
            }

            private static string EntityContextToString(DurableEntityContext arg)
            {
                // assemble the operation batch information
                var operationBatch = new JArray();
                foreach (var operation in arg.OperationBatch)
                {
                    operationBatch.Add(new JObject(
                        new JProperty("name", operation.Operation),
                        new JProperty("input", operation.Input),
                        new JProperty("isSignal", operation.IsSignal)));
                }

                // assemble the entity state information
                var contextObject = new JObject(
                    new JProperty("self", new JObject(
                        new JProperty("name", arg.Self.EntityName),
                        new JProperty("key", arg.Self.EntityKey))),
                    new JProperty("exists", arg.State.EntityExists),
                    new JProperty("state", arg.State.EntityState),
                    new JProperty("batch", operationBatch));

                return contextObject.ToString();
            }
        }
    }
}
