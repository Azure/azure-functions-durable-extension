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
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.OutOfProcOrchestrationShim;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OrchestrationTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly DurableTaskExtension config;
        private readonly string connectionName;

        public OrchestrationTriggerAttributeBindingProvider(
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
            OrchestrationTriggerAttribute? trigger = parameter.GetCustomAttribute<OrchestrationTriggerAttribute>(inherit: false);
            if (trigger == null)
            {
                return Task.FromResult<ITriggerBinding?>(null);
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
            var binding = new OrchestrationTriggerBinding(this.config, parameter, orchestratorName, this.connectionName);
            return Task.FromResult<ITriggerBinding?>(binding);
        }

        private class OrchestrationTriggerBinding : ITriggerBinding
        {
            private static readonly IReadOnlyDictionary<string, object?> EmptyBindingData = new Dictionary<string, object?>(capacity: 0);
            private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

            private readonly DurableTaskExtension config;
            private readonly ParameterInfo parameterInfo;
            private readonly FunctionName orchestratorName;
            private readonly string connectionName;

            public OrchestrationTriggerBinding(
                DurableTaskExtension config,
                ParameterInfo parameterInfo,
                FunctionName orchestratorName,
                string connectionName)
            {
                this.config = config;
                this.parameterInfo = parameterInfo;
                this.orchestratorName = orchestratorName;
                this.connectionName = connectionName;
                this.BindingDataContract = GetBindingDataContract(parameterInfo);
            }

            // Out-of-proc V2 uses a different trigger value type
            public Type TriggerValueType => this.config.OutOfProcProtocol == OutOfProcOrchestrationProtocol.MiddlewarePassthrough ?
                typeof(RemoteOrchestratorContext) :
                typeof(IDurableOrchestrationContext);

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

            public Task<ITriggerData> BindAsync(object? value, ValueBindingContext context)
            {
                if (value is DurableOrchestrationContext orchestrationContext)
                {
                    Type destinationType = this.parameterInfo.ParameterType;

                    object? convertedValue = null;
                    if (destinationType == typeof(IDurableOrchestrationContext))
                    {
                        convertedValue = orchestrationContext;
                    }
                    else if (this.config.TypedCodeProvider.IsInitialized &&
                        destinationType.Name == TypedCodeProvider.ITypedDurableOrchestrationContext)
                    {
                        convertedValue = this.config.TypedCodeProvider.InstantiateTypedDurableOrchestrationContext(orchestrationContext);
                    }
                    else if (destinationType == typeof(string))
                    {
                        convertedValue = OrchestrationContextToString(orchestrationContext);
                    }

                    var contextValueProvider = new ObjectValueProvider(
                        convertedValue ?? value,
                        this.parameterInfo.ParameterType);

                    var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        [this.parameterInfo.Name!] = convertedValue,
                    };

                    // We don't specify any return value binding because we process the return value
                    // earlier in the pipeline via the InvokeHandler extensibility.
                    var triggerData = new TriggerData(contextValueProvider, bindingData);
                    return Task.FromResult<ITriggerData>(triggerData);
                }
                else if (value is RemoteOrchestratorContext remoteContext)
                {
                    // Remote context is only for modern out-of-process function execution and
                    // contains a lighter payload.
                    string serializedContext = JsonConvert.SerializeObject(remoteContext, JsonSettings);
                    var contextValueProvider = new ObjectValueProvider(serializedContext, typeof(string));
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
                // DurableOrchestrationContextBase or some derivative. Non-.NET apps
                // which cannot use these types are therefore assumed to be "out-of-proc".
                // We may need to revisit this assumption when Functions v2 adds support
                // for "out-of-proc" .NET.
                var isOutOfProc = !typeof(IDurableOrchestrationContext).IsAssignableFrom(this.parameterInfo.ParameterType);
                this.config.RegisterOrchestrator(this.orchestratorName, new RegisteredFunctionInfo(context.Executor, isOutOfProc));

                var listener = new DurableTaskListener(
                    this.config,
                    context.Descriptor.Id,
                    this.orchestratorName,
                    FunctionType.Orchestrator,
                    this.connectionName);
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
                    new JProperty("parentInstanceId", arg.ParentInstanceId),
                    new JProperty("upperSchemaVersion", SchemaVersion.V2),
                    new JProperty("longRunningTimerIntervalDuration", arg.LongRunningTimerIntervalDuration),
                    new JProperty("maximumShortTimerDuration", arg.MaximumShortTimerDuration));
                return contextObject.ToString();
            }
        }
    }
}
