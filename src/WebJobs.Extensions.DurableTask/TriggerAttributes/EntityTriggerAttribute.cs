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

        /// <summary>
        /// Gets or sets a value indicating whether this entity requires gRPC communication. Default false.
        /// </summary>
        /// <remarks>
        /// If set to true, the Durable extension will start a gRPC server to handle communication for this entity,
        /// regardless of the detected worker runtime. This is used for languages migrating from HTTP to gRPC to allow the
        /// worker to communicate the desired connection type back to the host.
        /// </remarks>
        /// <value>
        /// True if gRPC is required; otherwise, false.
        /// </value>
        public bool DurableRequiresGrpc { get; set; }
    }
}
