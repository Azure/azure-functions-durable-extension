// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Trigger attribute used for durable actor functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{Actor} ({Version})")]
#if NETSTANDARD2_0
#pragma warning disable CS0618 // Type or member is obsolete
    [Binding(TriggerHandlesReturnValue = true)]
#pragma warning restore CS0618 // Type or member is obsolete
#else
    [Binding]
#endif
    public sealed class ActorTriggerAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the actor class.
        /// </summary>
        /// <remarks>
        /// If not specified, the function name is used as the name of the actor class.
        /// </remarks>
        /// <value>
        /// The name of the actor class or <c>null</c> to use the function name.
        /// </value>
#pragma warning disable CS0618 // Type or member is obsolete
        [AutoResolve]
#pragma warning restore CS0618 // Type or member is obsolete
        public string ActorClassName { get; set; }
    }
}
