// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
}
