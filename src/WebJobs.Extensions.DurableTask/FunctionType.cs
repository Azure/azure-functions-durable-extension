// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The type of a function.
    /// </summary>
    internal enum FunctionType
    {
        Activity,
        Orchestrator,
        Entity,
    }
}