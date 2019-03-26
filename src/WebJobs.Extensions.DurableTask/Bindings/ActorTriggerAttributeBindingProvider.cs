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
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ActorTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskExtension config;
        private readonly ExtensionConfigContext extensionContext;
        private readonly EndToEndTraceHelper traceHelper;

        public ActorTriggerAttributeBindingProvider(
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
            ActorTriggerAttribute trigger = parameter.GetCustomAttribute<ActorTriggerAttribute>(inherit: false);
            if (trigger == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // Priority for getting the name is [ActorTrigger], [FunctionName], method name
            string name = trigger.ActorClassName;
            if (string.IsNullOrEmpty(name))
            {
                MemberInfo method = context.Parameter.Member;
                name = method.GetCustomAttribute<FunctionNameAttribute>()?.Name ?? method.Name;
            }

            // The actor class name defaults to the method name.
            var actorClassName = new FunctionName(name);
            this.config.RegisterActor(actorClassName, null);
            var binding = new ActorTriggerBinding(this.config, parameter, actorClassName);
            return Task.FromResult<ITriggerBinding>(binding);
        }

        private class ActorTriggerBinding : ITriggerBinding
        {
            private readonly DurableTaskExtension config;
            private readonly ParameterInfo parameterInfo;
            private readonly FunctionName actorClassName;

            public ActorTriggerBinding(
                DurableTaskExtension config,
                ParameterInfo parameterInfo,
                FunctionName actorClassName)
            {
                this.config = config;
                this.parameterInfo = parameterInfo;
                this.actorClassName = actorClassName;
                this.BindingDataContract = GetBindingDataContract(parameterInfo);
            }

            public Type TriggerValueType => typeof(IDurableActorContext);

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
                var actorContext = (DurableActorContext)value;
                Type destinationType = this.parameterInfo.ParameterType;

                object convertedValue = null;
                if (destinationType == typeof(IDurableActorContext))
                {
                    convertedValue = actorContext;
                }
                else if (destinationType == typeof(string))
                {
                    convertedValue = ActorContextToString(actorContext);
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
                // IActorContext or some derivative. Non-.NET apps
                // which cannot use these types are therefore assumed to be "out-of-proc".
                // We may need to revisit this assumption when Functions v2 adds support
                // for "out-of-proc" .NET.
                var isOutOfProc = !typeof(IDurableActorContext).IsAssignableFrom(this.parameterInfo.ParameterType);
                this.config.RegisterActor(this.actorClassName, new RegisteredFunctionInfo(context.Executor, isOutOfProc));

                var listener = new DurableTaskListener(
                    this.config,
                    this.actorClassName,
                    context.Executor,
                    FunctionType.Actor);
                return Task.FromResult<IListener>(listener);
            }

            private static string ActorContextToString(DurableActorContext arg)
            {
                var history = JArray.FromObject(arg.History);

                // TODO figure out what exactly is needed here
                var contextObject = new JObject(
                    new JProperty("history", history),
                    new JProperty("actor", ((IDurableActorContext)arg).Self),
                    new JProperty("isReplaying", arg.IsReplaying));

                return contextObject.ToString();
            }
        }
    }
}
