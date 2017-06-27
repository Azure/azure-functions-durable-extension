// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ActivityTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskConfiguration durableTaskConfig;
        private readonly ExtensionConfigContext extensionContext;
        private readonly EndToEndTraceHelper traceHelper;

        public ActivityTriggerAttributeBindingProvider(
            DurableTaskConfiguration durableTaskConfig,
            ExtensionConfigContext extensionContext,
            EndToEndTraceHelper traceHelper)
        {
            this.durableTaskConfig = durableTaskConfig;
            this.extensionContext = extensionContext;
            this.traceHelper = traceHelper;

            ActivityTriggerBinding.RegisterBindingRules(extensionContext.Config);
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ParameterInfo parameter = context.Parameter;
            ActivityTriggerAttribute trigger = parameter.GetCustomAttribute<ActivityTriggerAttribute>(inherit: false);
            if (trigger == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // The activity name defaults to the method name.
            string activityName = trigger.Activity ?? parameter.Member.Name;

            var binding = new ActivityTriggerBinding(
                this,
                parameter,
                trigger,
                activityName,
                trigger.Version);
            return Task.FromResult<ITriggerBinding>(binding);
        }

        private class ActivityTriggerBinding : ITriggerBinding
        {
            private static readonly IReadOnlyDictionary<string, Type> StaticBindingContract =
                new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
                {
                    { nameof(DurableActivityContext.InstanceId), typeof(string) }
                };

            private readonly ActivityTriggerAttributeBindingProvider parent;
            private readonly ParameterInfo parameterInfo;
            private readonly ActivityTriggerAttribute attribute;
            private readonly string activityName;
            private readonly string activityVersion;

            public ActivityTriggerBinding(
                ActivityTriggerAttributeBindingProvider parent,
                ParameterInfo parameterInfo,
                ActivityTriggerAttribute attribute,
                string activityName,
                string activityVersion)
            {
                this.parent = parent;
                this.parameterInfo = parameterInfo;
                this.attribute = attribute;
                this.activityName = activityName;
                this.activityVersion = activityVersion;
            }

            public Type TriggerValueType => typeof(DurableActivityContext);

            public IReadOnlyDictionary<string, Type> BindingDataContract => StaticBindingContract;

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                IConverterManager cm = this.parent.extensionContext.Config.ConverterManager;
                MethodInfo getConverterMethod = cm.GetType().GetMethod(nameof(cm.GetConverter));
                getConverterMethod = getConverterMethod.MakeGenericMethod(
                    typeof(DurableActivityContext),
                    this.parameterInfo.ParameterType,
                    typeof(ActivityTriggerAttribute));

                Delegate d = (Delegate)getConverterMethod.Invoke(cm, null);
                object convertedValue = d.DynamicInvoke(value, this.attribute, context);

                var valueProvider = new ObjectValueProvider(
                    convertedValue,
                    this.parameterInfo.ParameterType);

                DurableActivityContext activityContext = (DurableActivityContext)value;
                var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { nameof(DurableActivityContext.InstanceId), activityContext.InstanceId }
                };

                var triggerData = new TriggerData(valueProvider, bindingData);
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

                var listener = DurableTaskListener.CreateForActivity(
                    this.parent.durableTaskConfig,
                    this.activityName,
                    this.activityVersion,
                    context.Executor);
                return Task.FromResult<IListener>(listener);
            }

            public static void RegisterBindingRules(JobHostConfiguration hostConfig)
            {
                IConverterManager cm = hostConfig.ConverterManager;
                cm.AddConverter<DurableActivityContext, string>(ActivityContextToString);
                cm.AddConverter<DurableActivityContext, JObject>(ActivityContextToJObject);

                // TODO: Add support for open types - i.e. POCO objects
            }

            private static JObject ActivityContextToJObject(DurableActivityContext arg)
            {
                return JObject.Parse(arg.GetRawInput());
            }

            private static string ActivityContextToString(DurableActivityContext arg)
            {
                return arg.GetInput<string>();
            }
        }
    }
}
