// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Trigger attribute used for durable orchestrator functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{Orchestration}")]
#pragma warning disable CS0618 // Type or member is obsolete
    [Binding(TriggerHandlesReturnValue = true)]
#pragma warning restore CS0618 // Type or member is obsolete
    public sealed class OrchestrationTriggerAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the orchestrator function.
        /// </summary>
        /// <remarks>
        /// If not specified, the function name is used as the name of the orchestration.
        /// </remarks>
        /// <value>
        /// The name of the orchestrator function or <c>null</c> to use the function name.
        /// </value>
#pragma warning disable CS0618 // Type or member is obsolete
        [AutoResolve]
#pragma warning restore CS0618 // Type or member is obsolete
        public string Orchestration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this orchestration requires gRPC communication. Default false.
        /// </summary>
        /// <remarks>
        /// If set to true, the Durable extension will start a gRPC server to handle communication for this orchestration,
        /// regardless of the detected worker runtime. This is used for languages migrating from HTTP to gRPC to allow the
        /// worker to communicate the desired connection type back to the host.
        /// </remarks>
        /// <value>
        /// True if gRPC is required; otherwise, false.
        /// </value>
        public bool DurableRequiresGrpc { get; set; }
    }
}
