﻿// Copyright (c) .NET Foundation. All rights reserved.
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
    [DebuggerDisplay("{Activity}")]
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
    }
}
