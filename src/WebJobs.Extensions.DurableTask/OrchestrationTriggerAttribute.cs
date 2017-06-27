// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Trigger attribute used for durable orchestrator functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{Orchestration} ({Version})")]
    [Binding]
    public sealed class OrchestrationTriggerAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the orchestrator function.
        /// </summary>
        /// <value>
        /// The name of the orchestrator function or <c>null</c> to use the function name.
        /// </value>
        public string Orchestration { get; set; }

        /// <summary>
        /// Gets or sets the version of the orchestrator function.
        /// </summary>
        /// <value>
        /// The version of the orchestrator function.
        /// </value>
        public string Version { get; set; }

        // Remove this with https://github.com/Azure/azure-webjobs-sdk/issues/1104 
        internal static void ApplyReturn(object context, object returnValue)
        {
            ((DurableOrchestrationContext)context).SetOutput(returnValue);
        }
    }
}
