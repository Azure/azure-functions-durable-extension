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

            public Type TriggerValueType => typeof(IDurableEntityContext);

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
                var entityContext = (DurableEntityContext)value;
                Type destinationType = this.parameterInfo.ParameterType;

                object? convertedValue = null;
                if (destinationType == typeof(IDurableEntityContext))
                {
                    convertedValue = entityContext;
#if !FUNCTIONS_V1
                    ((IDurableEntityContext)value).FunctionBindingContext = context.FunctionContext;
#endif
                }
                else if (destinationType == typeof(string))
                {
                    // DRAFT
                    // convertedValue = EntityContextToString(entityContext);
                }

                var inputValueProvider = new ObjectValueProvider(
                    convertedValue ?? value,
                    this.parameterInfo.ParameterType);

                var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                bindingData[this.parameterInfo.Name!] = convertedValue;

                var triggerData = new TriggerData(inputValueProvider, bindingData);
                return Task.FromResult<ITriggerData>(triggerData);
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

            // DRAFT deleted OOPROC format
        }
    }
}
