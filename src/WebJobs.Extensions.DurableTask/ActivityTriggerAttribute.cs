// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Trigger attribute used for durable activity functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{Activity} ({Version})")]
    [Binding]
    public sealed class ActivityTriggerAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the activity function.
        /// </summary>
        /// <value>
        /// The name of the activity function or <c>null</c> to use the function name.
        /// </value>
        public string Activity { get; set; }

        /// <summary>
        /// Gets or sets the version of the activity function.
        /// </summary>
        /// <value>
        /// The version of the activity function.
        /// </value>
        public string Version { get; set; }

        // Remove this with https://github.com/Azure/azure-webjobs-sdk/issues/1104 
        internal static void ApplyReturn(object context, object returnValue)
        {
            ((DurableActivityContext)context).SetOutput(returnValue);
        }
    }
}
