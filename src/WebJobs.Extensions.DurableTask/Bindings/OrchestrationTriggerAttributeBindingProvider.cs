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

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OrchestrationTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskExtension config;
        private readonly ExtensionConfigContext extensionContext;
        private readonly EndToEndTraceHelper traceHelper;

        public OrchestrationTriggerAttributeBindingProvider(
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

            // The orchestration name defaults to the method name.
            var orchestratorName = new FunctionName(name, trigger.Version);
            var binding = new OrchestrationTriggerBinding(this.config, parameter, orchestratorName);
            return Task.FromResult<ITriggerBinding>(binding);
        }

        private class OrchestrationTriggerBinding : ITriggerBinding
        {
            private static readonly IReadOnlyDictionary<string, Type> StaticBindingContract =
                new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
                {
                    // This binding supports return values of any type
                    { "$return", typeof(object).MakeByRefType() },
                };

            private readonly DurableTaskExtension config;
            private readonly ParameterInfo parameterInfo;
            private readonly FunctionName orchestratorName;

            public OrchestrationTriggerBinding(
                DurableTaskExtension config,
                ParameterInfo parameterInfo,
                FunctionName orchestratorName)
            {
                this.config = config;
                this.parameterInfo = parameterInfo;
                this.orchestratorName = orchestratorName;
            }

            public Type TriggerValueType => typeof(DurableOrchestrationContext);

            public IReadOnlyDictionary<string, Type> BindingDataContract => StaticBindingContract;

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                // No conversions
                var inputValueProvider = new ObjectValueProvider(value, this.TriggerValueType);

                // We don't specify any return value binding because we process the return value
                // earlier in the pipeline via the InvokeHandler extensibility.
                var triggerData = new TriggerData(inputValueProvider, bindingData: null);
                return Task.FromResult<ITriggerData>(triggerData);
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "The caller is responsible for disposing")]
            public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                var listener = new DurableTaskListener(
                    this.config,
                    this.orchestratorName,
                    context.Executor,
                    isOrchestrator: true);
                return Task.FromResult<IListener>(listener);
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor { Name = this.parameterInfo.Name };
            }
        }
    }
}
