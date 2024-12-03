// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Trigger attribute used for durable entity functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{EntityName} ({Version})")]
#pragma warning disable CS0618 // Type or member is obsolete
    [Binding(TriggerHandlesReturnValue = true)]
#pragma warning restore CS0618 // Type or member is obsolete
    public sealed class EntityTriggerAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the entity.
        /// </summary>
        /// <remarks>
        /// If not specified, the function name is used as the name of the entity.
        /// </remarks>
        /// <value>
        /// The name of the entity or <c>null</c> to use the function name.
        /// </value>
#pragma warning disable CS0618 // Type or member is obsolete
        [AutoResolve]
#pragma warning restore CS0618 // Type or member is obsolete
        public string EntityName { get; set; }
    }
}
