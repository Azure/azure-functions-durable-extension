// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Formerly, the abstract base class for DurableOrchestrationContext.
    /// Now obsolete: use <see cref="IDurableOrchestrationContext"/> instead.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1302", Justification = "Engineered for v1 legacy compatibility.")]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649", Justification = "Engineered for v1 legacy compatibility.")]
    [Obsolete("Use IDurableOrchestrationContext instead.")]
    public interface DurableOrchestrationContextBase : IDurableOrchestrationContext
    {
    }

    /// <summary>
    /// Formerly, the abstract base class for DurableActivityContext.
    /// Now obsolete: use <see cref="IDurableActivityContext"/> instead.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1302", Justification = "Engineered for v1 legacy compatibility.")]
    [Obsolete("Use IDurableActivityContext instead.")]
    public interface DurableActivityContextBase : IDurableActivityContext
    {
    }

    /// <summary>
    /// Formerly, the abstract base class for DurableOrchestrationClient.
    /// Now obsolete: use <see cref="IDurableOrchestrationClient"/> instead.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1302", Justification = "Engineered for v1 legacy compatibility.")]
    [Obsolete("Use IDurableOrchestrationClient instead.")]
    public interface DurableOrchestrationClientBase : IDurableOrchestrationClient
    {
    }
}
