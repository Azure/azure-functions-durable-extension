// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Trigger attribute used for durable activity functions.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
[DebuggerDisplay("{Activity}")]
[InputConverter(typeof(ActivityInputConverter))]
[ConverterFallbackBehavior(ConverterFallbackBehavior.Allow)]
public sealed class ActivityTriggerAttribute : TriggerBindingAttribute
{
    /// <summary>
    /// Gets or sets the name of the activity function.
    /// </summary>
    /// <remarks>
    /// If not specified, the function name is used as the name of the activity.
    /// This property supports binding parameters.
    /// </remarks>
    /// <value>
    /// The name of the activity function or <c>null</c> to use the function name.
    /// </value>
    public string? Activity { get; set; }
}
