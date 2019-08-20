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
    internal class OrchestrationTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskExtensionBase config;
        private readonly ExtensionConfigContext extensionContext;
        private readonly EndToEndTraceHelper traceHelper;

        public OrchestrationTriggerAttributeBindingProvider(
            DurableTaskExtensionBase config,
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
            OrchestrationTriggerAttribute trigger = parameter.GetCustomAttribute<OrchestrationTriggerAttribute>(inherit: false);
            if (trigger == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // Priority for getting the name is [OrchestrationTrigger], [FunctionName], method name
            string name = trigger.Orchestration;
            if (string.IsNullOrEmpty(name))
            {
                MemberInfo method = context.Parameter.Member;
                name = method.GetCustomAttribute<FunctionNameAttribute>()?.Name ?? method.Name;
            }

            var orchestratorName = new FunctionName(name);
            if (name.StartsWith("@"))
            {
                throw new ArgumentException("Orchestration names must not start with @.");
            }

            this.config.RegisterOrchestrator(orchestratorName, null);
            var binding = new OrchestrationTriggerBinding(this.config, parameter, orchestratorName);
            return Task.FromResult<ITriggerBinding>(binding);
        }

        private class OrchestrationTriggerBinding : ITriggerBinding
        {
            private readonly DurableTaskExtensionBase config;
            private readonly ParameterInfo parameterInfo;
            private readonly FunctionName orchestratorName;

            public OrchestrationTriggerBinding(
                DurableTaskExtensionBase config,
                ParameterInfo parameterInfo,
                FunctionName orchestratorName)
            {
                this.config = config;
                this.parameterInfo = parameterInfo;
                this.orchestratorName = orchestratorName;
                this.BindingDataContract = GetBindingDataContract(parameterInfo);
            }

            public Type TriggerValueType => typeof(IDurableOrchestrationContext);

            public IReadOnlyDictionary<string, Type> BindingDataContract { get; }

            private static IReadOnlyDictionary<string, Type> GetBindingDataContract(ParameterInfo parameterInfo)
            {
                var contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
                {
                    // This binding supports return values of any type
                    { "$return", typeof(object).MakeByRefType() },
                };

                // allow binding to the parameter name
                contract[parameterInfo.Name] = parameterInfo.ParameterType;

                return contract;
            }

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                var orchestrationContext = (DurableOrchestrationContext)value;
                Type destinationType = this.parameterInfo.ParameterType;

                object convertedValue = null;
                if (destinationType == typeof(IDurableOrchestrationContext))
                {
                    convertedValue = orchestrationContext;
                }
                else if (destinationType == typeof(string))
                {
                    convertedValue = OrchestrationContextToString(orchestrationContext);
                }

                var inputValueProvider = new ObjectValueProvider(
                    convertedValue ?? value,
                    this.parameterInfo.ParameterType);

                var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    [this.parameterInfo.Name] = convertedValue,
                };

                // We don't specify any return value binding because we process the return value
                // earlier in the pipeline via the InvokeHandler extensibility.
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
                // DurableOrchestrationContextBase or some derivative. Non-.NET apps
                // which cannot use these types are therefore assumed to be "out-of-proc".
                // We may need to revisit this assumption when Functions v2 adds support
                // for "out-of-proc" .NET.
                var isOutOfProc = !typeof(IDurableOrchestrationContext).IsAssignableFrom(this.parameterInfo.ParameterType);
                this.config.RegisterOrchestrator(this.orchestratorName, new RegisteredFunctionInfo(context.Executor, isOutOfProc));

                var listener = new DurableTaskListener(
                    this.config,
                    this.orchestratorName,
                    context.Executor,
                    FunctionType.Orchestrator);
                return Task.FromResult<IListener>(listener);
            }

            private static string OrchestrationContextToString(DurableOrchestrationContext arg)
            {
                var history = JArray.FromObject(arg.History);
                var input = arg.GetInputAsJson();

                var contextObject = new JObject(
                    new JProperty("history", history),
                    new JProperty("input", input),
                    new JProperty("instanceId", arg.InstanceId),
                    new JProperty("isReplaying", arg.IsReplaying),
                    new JProperty("parentInstanceId", arg.ParentInstanceId));
                return contextObject.ToString();
            }
        }
    }
}
