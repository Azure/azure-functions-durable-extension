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

        /// <summary>
        /// Gets or sets a value indicating whether a parameter declared as <c>object</c> binds to the
        /// activity input. Default false.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This setting only affects parameters declared as <c>object</c> or <c>dynamic</c> (which the
        /// runtime also sees as <c>object</c>). It is ignored for every other parameter type, because all
        /// other types already bind to the activity input.
        /// </para>
        /// <para>
        /// For historical reasons, an <c>object</c> parameter binds to the <see cref="IDurableActivityContext"/>
        /// rather than to the activity input, which is surprising and inconsistent with every other parameter
        /// type. See https://github.com/Azure/azure-functions-durable-extension/issues/1343. Setting this
        /// property to true opts a single activity function into the corrected behavior. The default of false
        /// preserves the legacy behavior so that existing applications are unaffected.
        /// </para>
        /// <para>
        /// The default is expected to change in the next major version of this extension, at which point this
        /// property becomes obsolete and declaring a parameter of type <see cref="IDurableActivityContext"/>
        /// will be the way to receive the activity context.
        /// </para>
        /// </remarks>
        /// <value>
        /// True to bind an <c>object</c> parameter to the activity input; otherwise, false to bind it to the
        /// <see cref="IDurableActivityContext"/>.
        /// </value>
        public bool BindToInput { get; set; }
    }
}
