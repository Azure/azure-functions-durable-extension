// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Trigger attribute used for durable entity functions.
/// </summary>
/// <remarks>
/// Entity triggers must bind to <see cref="TaskEntityDispatcher"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
[DebuggerDisplay("{EntityName}")]
public sealed class EntityTriggerAttribute : TriggerBindingAttribute
{
    /// <summary>
    /// Gets or sets the name of the entity function.
    /// </summary>
    /// <remarks>
    /// If not specified, the function name is used as the name of the entity.
    /// This property supports binding parameters.
    /// </remarks>
    /// <value>
    /// The name of the entity function or <c>null</c> to use the function name.
    /// </value>
    public string? EntityName { get; set; }
}
