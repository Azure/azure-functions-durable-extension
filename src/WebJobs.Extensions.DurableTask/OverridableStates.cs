// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Represents options for different states that an existing orchestrator can be in to be able to be overwritten by
    /// an attempt to start a new instance with the same instance Id.
    /// </summary>
    public enum OverridableStates
    {
        /// <summary>
        /// Option to start a new orchestrator instance with an existing instnace Id when the existing
        /// instance is in any state.
        /// </summary>
        AnyState,

        /// <summary>
        /// Option to only start a new orchestrator instance with an existing instance Id when the existing
        /// instance is in a terminated, failed, or completed state.
        /// </summary>
        NonRunningStates,
    }

    /// <summary>
    /// Extension methods for <see cref="OverridableStates"/>.
    /// </summary>
#pragma warning disable SA1649 // File name should match first type name Justification: pairing extension methods with enum.
    internal static class OverridableStatesExtensions
#pragma warning restore SA1649 // File name should match first type name
    {
        private static readonly OrchestrationStatus[] NonRunning = new OrchestrationStatus[]
        {
            OrchestrationStatus.Running,
            OrchestrationStatus.ContinuedAsNew,
            OrchestrationStatus.Pending,
            OrchestrationStatus.Suspended,
        };

        /// <summary>
        /// Gets the dedupe <see cref="OrchestrationStatus"/> for a given <see cref="OverridableStates"/>.
        /// </summary>
        /// <param name="states">The overridable states.</param>
        /// <returns>An array of statuses to dedupe.</returns>
        public static OrchestrationStatus[] ToDedupeStatuses(this OverridableStates states)
        {
            return states switch
            {
                OverridableStates.NonRunningStates => NonRunning,
                _ => Array.Empty<OrchestrationStatus>(),
            };
        }
    }
}
