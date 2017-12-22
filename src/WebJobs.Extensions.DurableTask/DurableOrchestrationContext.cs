// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Parameter data for orchestration bindings that can be used to schedule function-based activities.
    /// </summary>
    public sealed class DurableOrchestrationContext : DurableOrchestrationContextBase
    {
        internal DurableOrchestrationContext(
            DurableTaskExtension config,
            string functionName,
            string functionVersion) : base(
            config,
            functionName,
            functionVersion)
        {
        }

        internal bool IsCompleted { get; set; }

        internal string HubName => this.config.HubName;

        internal string Name => this.orchestrationName;

        internal string Version => this.orchestrationVersion;

        internal bool IsOutputSet => this.serializedOutput != null;

        internal void AssignToCurrentThread()
        {
            this.owningThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        internal void SetInput(OrchestrationContext frameworkContext, string rawInput)
        {
            this.innerContext = frameworkContext;
            this.serializedInput = rawInput;
        }

        /// <summary>
        /// Sets the JSON-serializeable output of the current orchestrator function.
        /// </summary>
        /// <remarks>
        /// If this method is not called explicitly, the return value of the orchestrator function is used as the output.
        /// </remarks>
        /// <param name="output">The JSON-serializeable value to use as the orchestrator function output.</param>
        internal void SetOutput(object output)
        {
            this.ThrowIfInvalidAccess();

            if (this.IsOutputSet)
            {
                throw new InvalidOperationException("The output has already been set of this orchestration instance.");
            }

            if (output != null)
            {
                JToken json = output as JToken;
                if (json != null)
                {
                    this.serializedOutput = json.ToString(Formatting.None);
                }
                else
                {
                    this.serializedOutput = MessagePayloadDataConverter.Default.Serialize(output);
                }
            }
            else
            {
                this.serializedOutput = null;
            }
        }

        internal string GetSerializedOutput()
        {
            return this.serializedOutput;
        }

        internal void RaiseEvent(string name, string input)
        {
            lock (this.pendingExternalEvents)
            {
                object tcs;
                if (this.pendingExternalEvents.TryGetValue(name, out tcs))
                {
                    Type tcsType = tcs.GetType();
                    Type genericTypeArgument = tcsType.GetGenericArguments()[0];

                    object deserializedObject = MessagePayloadDataConverter.Default.Deserialize(input, genericTypeArgument);
                    MethodInfo trySetResult = tcsType.GetMethod("TrySetResult");
                    trySetResult.Invoke(tcs, new[] { deserializedObject });
                }
            }
        }
    }
}
