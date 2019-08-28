// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
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
        private readonly ExtensionConfigContext extensionContext;
        private readonly EndToEndTraceHelper traceHelper;

        public EntityTriggerAttributeBindingProvider(
            DurableTaskExtension config,
            ExtensionConfigContext extensionContext,
            EndToEndTraceHelper traceHelper)
        {
            this.config = config;
            this.extensionContext = extensionContext;
            this.traceHelper = traceHelper;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ParameterInfo parameter = context.Parameter;
            EntityTriggerAttribute trigger = parameter.GetCustomAttribute<EntityTriggerAttribute>(inherit: false);
            if (trigger == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
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
            var binding = new EntityTriggerBinding(this.config, parameter, entityName);
            return Task.FromResult<ITriggerBinding>(binding);
        }

        private class EntityTriggerBinding : ITriggerBinding
        {
            private readonly DurableTaskExtension config;
            private readonly ParameterInfo parameterInfo;
            private readonly FunctionName entityName;

            public EntityTriggerBinding(
                DurableTaskExtension config,
                ParameterInfo parameterInfo,
                FunctionName entityName)
            {
                this.config = config;
                this.parameterInfo = parameterInfo;
                this.entityName = entityName;
                this.BindingDataContract = GetBindingDataContract(parameterInfo);
            }

            public Type TriggerValueType => typeof(IDurableEntityContext);

            public IReadOnlyDictionary<string, Type> BindingDataContract { get; }

            private static IReadOnlyDictionary<string, Type> GetBindingDataContract(ParameterInfo parameterInfo)
            {
                var contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

                // allow binding to the parameter name
                contract[parameterInfo.Name] = parameterInfo.ParameterType;

                // do not allow any return types (other than void)

                return contract;
            }

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                var entityContext = (DurableEntityContext)value;
                Type destinationType = this.parameterInfo.ParameterType;

                object convertedValue = null;
                if (destinationType == typeof(IDurableEntityContext))
                {
                    convertedValue = entityContext;
#if NETSTANDARD2_0
                    ((IDurableEntityContext)value).FunctionBindingContext = context.FunctionContext;
#endif
                }
                else if (destinationType == typeof(string))
                {
                    convertedValue = EntityContextToString(entityContext);
                }

                var inputValueProvider = new ObjectValueProvider(
                    convertedValue ?? value,
                    this.parameterInfo.ParameterType);

                var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                bindingData[this.parameterInfo.Name] = convertedValue;

                var triggerData = new TriggerData(inputValueProvider, bindingData);
                return Task.FromResult<ITriggerData>(triggerData);
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor { Name = this.parameterInfo.Name };
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "The caller is responsible for disposing")]
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
                    this.entityName,
                    context.Executor,
                    FunctionType.Entity);
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
