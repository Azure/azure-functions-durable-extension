// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ActivityTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskExtension durableTaskConfig;
        private readonly string connectionName;

        public ActivityTriggerAttributeBindingProvider(
            DurableTaskExtension durableTaskConfig,
            string connectionName)
        {
            this.durableTaskConfig = durableTaskConfig;
            this.connectionName = connectionName;
        }

        public Task<ITriggerBinding?> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ParameterInfo parameter = context.Parameter;
            ActivityTriggerAttribute? trigger = parameter.GetCustomAttribute<ActivityTriggerAttribute>(inherit: false);
            if (trigger == null)
            {
                return Task.FromResult<ITriggerBinding?>(null);
            }

            // Priority for getting the name is [ActivityTrigger], [FunctionName], method name
            string name = trigger.Activity;
            if (string.IsNullOrEmpty(name))
            {
                MemberInfo method = context.Parameter.Member;
                name = method.GetCustomAttribute<FunctionNameAttribute>()?.Name ?? method.Name;
            }

            // The activity name defaults to the method name.
            var activityName = new FunctionName(name);
            this.durableTaskConfig.RegisterActivity(activityName, null);
            var binding = new ActivityTriggerBinding(this, parameter, trigger, activityName, this.durableTaskConfig);
            return Task.FromResult<ITriggerBinding?>(binding);
        }

        private class ActivityTriggerBinding : ITriggerBinding
        {
            private const string InstanceIdBindingPropertyName = "instanceId";
            private const string DataBindingPropertyName = "data";

            private readonly ActivityTriggerAttributeBindingProvider parent;
            private readonly ParameterInfo parameterInfo;
            private readonly ActivityTriggerAttribute attribute;
            private readonly FunctionName activityName;
            private readonly IReadOnlyDictionary<string, Type> contract;
            private readonly DurableTaskExtension durableTaskConfig;

            public ActivityTriggerBinding(
                ActivityTriggerAttributeBindingProvider parent,
                ParameterInfo parameterInfo,
                ActivityTriggerAttribute attribute,
                FunctionName activity,
                DurableTaskExtension durableTaskConfig)
            {
                this.parent = parent;
                this.parameterInfo = parameterInfo;
                this.attribute = attribute;
                this.activityName = activity;
                this.contract = GetBindingDataContract(parameterInfo);
                this.durableTaskConfig = durableTaskConfig;
            }

            public Type TriggerValueType => typeof(IDurableActivityContext);

            public IReadOnlyDictionary<string, Type> BindingDataContract => this.contract;

            private static IReadOnlyDictionary<string, Type> GetBindingDataContract(ParameterInfo parameterInfo)
            {
                var contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
                {
                    // This binding supports return values of any type
                    { "$return", typeof(object).MakeByRefType() },
                    { InstanceIdBindingPropertyName, typeof(string) },
                };

                // allow binding to the parameter name
                contract[parameterInfo.Name!] = parameterInfo.ParameterType;

                // allow binding directly to the JSON representation of the data.
                contract[DataBindingPropertyName] = typeof(JValue);

                return contract;
            }

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                // If we are not directly passed a DurableActivityContext, we can assume we are being called directly
                // by the admin API. This is mainly used for the Azure Portal execution scenario.
                if (value is not DurableActivityContext activityContext)
                {
                    if (value is not string serializedInput)
                    {
                        throw new InvalidOperationException($"Cannot execute an Activity Trigger without a {nameof(DurableActivityContext)} or a {nameof(String)} that represents the serialized input.");
                    }

                    // Durable functions expects input as a JArray with one element.
                    serializedInput = $"[{serializedInput}]";

                    activityContext = new DurableActivityContext(this.durableTaskConfig, Guid.NewGuid().ToString(), serializedInput, this.activityName.Name);
                }

                Type destinationType = this.parameterInfo.ParameterType;

                object? convertedValue;
                if (destinationType == typeof(object))
                {
                    convertedValue = value;
                }
                else if (destinationType == typeof(IDurableActivityContext))
                {
                    convertedValue = activityContext;
                }
                else if (destinationType == typeof(JObject))
                {
                    convertedValue = ActivityContextToJObject(activityContext);
                }
                else
                {
                    convertedValue = activityContext.GetInput(destinationType);
                }

                var inputValueProvider = new ObjectValueProvider(
                    convertedValue,
                    this.parameterInfo.ParameterType);

                // Note that there could be conflicts in thiese dictionary keys, in which case
                // the order here determines which binding rule will win.
                var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                bindingData[InstanceIdBindingPropertyName] = ((IDurableActivityContext)activityContext).InstanceId;
                bindingData[this.parameterInfo.Name!] = convertedValue;
                bindingData[DataBindingPropertyName] = activityContext.GetInputAsJson();

                var triggerData = new TriggerData(inputValueProvider, bindingData);
                triggerData.ReturnValueProvider = new ActivityTriggerReturnValueBinder(
                    activityContext,
                    this.parameterInfo.ParameterType);

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

                this.parent.durableTaskConfig.RegisterActivity(this.activityName, context.Executor);

                var listener = new DurableTaskListener(
                    this.parent.durableTaskConfig,
                    context.Descriptor.Id,
                    this.activityName,
                    FunctionType.Activity,
                    this.parent.connectionName);
                return Task.FromResult<IListener>(listener);
            }

            private static JObject? ActivityContextToJObject(IDurableActivityContext arg)
            {
                JToken token = ((DurableActivityContext)arg).GetInputAsJson();
                if (token == null)
                {
                    return null;
                }

                JObject? jObj = token as JObject;
                if (jObj == null)
                {
                    throw new ArgumentException($"Cannot convert '{token}' to a JSON object.");
                }

                return jObj;
            }

            private class ActivityTriggerReturnValueBinder : IValueBinder
            {
                private readonly DurableActivityContext context;
                private readonly Type valueType;

                public ActivityTriggerReturnValueBinder(DurableActivityContext context, Type valueType)
                {
                    this.context = context ?? throw new ArgumentNullException(nameof(context));
                    this.valueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
                }

                public Type Type => this.valueType;

                public Task<object> GetValueAsync()
                {
                    throw new NotImplementedException("This binder should only be used for setting return values!");
                }

                public Task SetValueAsync(object value, CancellationToken cancellationToken)
                {
                    if (value != null)
                    {
                        this.context.SetOutput(value);
                    }

                    return Task.CompletedTask;
                }

                public string ToInvokeString()
                {
                    return this.context.GetSerializedOutput();
                }
            }
        }
    }
}
