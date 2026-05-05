// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.DurableTask.Worker.Middleware;

/// <summary>
/// Extension methods for Durable Task middleware contexts in Azure Functions.
/// </summary>
public static class DurableTaskMiddlewareContextExtensionMethods
{
    /// <summary>
    /// Gets the Azure Functions <see cref="FunctionContext"/> associated with the current orchestration middleware
    /// invocation.
    /// </summary>
    /// <param name="context">The orchestration middleware context.</param>
    /// <returns>The Azure Functions context if available; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
    public static FunctionContext? GetFunctionContext(this TaskOrchestrationMiddlewareContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.Features.Get<FunctionContext>();
    }

    /// <summary>
    /// Gets the Azure Functions <see cref="FunctionContext"/> associated with the current activity middleware
    /// invocation.
    /// </summary>
    /// <param name="context">The activity middleware context.</param>
    /// <returns>The Azure Functions context if available; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
    public static FunctionContext? GetFunctionContext(this TaskActivityMiddlewareContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.Features.Get<FunctionContext>();
    }
}
