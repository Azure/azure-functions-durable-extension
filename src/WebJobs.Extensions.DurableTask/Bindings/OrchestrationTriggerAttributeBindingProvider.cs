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

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OrchestrationTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskConfiguration config;
        private readonly ExtensionConfigContext extensionContext;
        private readonly EndToEndTraceHelper traceHelper;

        public OrchestrationTriggerAttributeBindingProvider(
            DurableTaskConfiguration config,
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

            // The orchestration name defaults to the method name.
            string orchestrationName = trigger.Orchestration ?? parameter.Member.Name;

            // TODO: Support for per-function connection string and task hub names
            var binding = new OrchestrationTriggerBinding(
                this.config,
                parameter,
                orchestrationName,
                trigger.Version);
            return Task.FromResult<ITriggerBinding>(binding);
        }

        private class OrchestrationTriggerBinding : ITriggerBinding
        {
            private readonly DurableTaskConfiguration config;
            private readonly ParameterInfo parameterInfo;
            private readonly string orchestrationName;
            private readonly string version;

            public OrchestrationTriggerBinding(
                DurableTaskConfiguration config,
                ParameterInfo parameterInfo,
                string orchestrationName,
                string version)
            {
                this.config = config;
                this.parameterInfo = parameterInfo;
                this.orchestrationName = orchestrationName;
                this.version = version;
            }

            public Type TriggerValueType => typeof(DurableOrchestrationContext);

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                // TODO: Figure out how or whether other types of bindings could be used for this trigger.
                get { return null; }
            }

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                // No conversions
                return Task.FromResult<ITriggerData>(new TriggerData(new ObjectValueProvider(value, this.TriggerValueType), null));
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
            public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                var listener = DurableTaskListener.CreateForOrchestration(
                    this.config,
                    this.orchestrationName,
                    this.version,
                    context.Executor);
                return Task.FromResult<IListener>(listener);
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor { Name = this.parameterInfo.Name };
            }
        }
    }
}
