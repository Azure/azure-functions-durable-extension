// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Deprecated attribute to bind a function parameter to a <see cref="IDurableClient"/>.
    /// Here for backwards compatibility. Please use the <see cref="DurableClientAttribute"/> instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    [Obsolete("OrchestrationClientAttribute is obsolete. Use DurableClientAttribute instead.")]
    public sealed class OrchestrationClientAttribute : DurableClientAttribute
    {
    }
}