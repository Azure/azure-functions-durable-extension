// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Trigger attribute used for durable activity functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{Activity}")]
#pragma warning disable CS0618 // Type or member is obsolete
    [Binding(TriggerHandlesReturnValue = true)]
#pragma warning restore CS0618 // Type or member is obsolete
    public sealed class ActivityTriggerAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the activity function.
        /// </summary>
        /// <value>
        /// The name of the activity function or <c>null</c> to use the function name.
        /// </value>
#pragma warning disable CS0618 // Type or member is obsolete
        [AutoResolve]
#pragma warning restore CS0618 // Type or member is obsolete
        public string Activity { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this activity requires gRPC communication. Default false.
        /// </summary>
        /// <remarks>
        /// If set to true, the Durable extension will start a gRPC server to handle communication for this activity,
        /// regardless of the detected worker runtime. This is used for languages migrating from HTTP to gRPC to allow the
        /// worker to communicate the desired connection type back to the host.
        /// </remarks>
        /// <value>
        /// True if gRPC is required; otherwise, false.
        /// </value>
        public bool DurableRequiresGrpc { get; set; }
    }
}
