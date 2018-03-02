// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Trigger attribute used for durable activity functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{Activity} ({Version})")]
#if NETSTANDARD2_0
    [Binding(TriggerHandlesReturnValue = true)]
#else
    [Binding]
#endif

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

        // Remove this with https://github.com/Azure/azure-webjobs-sdk-script/issues/1422
        internal static void ApplyReturn(object context, object returnValue)
        {
            DurableActivityContext activityContext = context as DurableActivityContext;
            if (activityContext == null)
            {
                throw new InvalidOperationException($"Only .NET {nameof(DurableActivityContext)} trigger parameters are supported at this time.");
            }

            activityContext.SetOutput(returnValue);
        }
    }
}
