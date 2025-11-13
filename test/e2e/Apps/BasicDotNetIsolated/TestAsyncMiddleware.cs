// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Microsoft.Azure.Durable.Tests.E2E;

/// <summary>
/// Test middleware that performs async operations to validate the fix for
/// https://github.com/microsoft/durabletask-dotnet/issues/158
/// This middleware simulates async behaviors like App Configuration or logging services.
/// </summary>
public class TestAsyncMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Simulate an async operation like fetching configuration or logging
        await Task.Delay(1);

        // Store metadata to verify the middleware ran
        context.Items["AsyncMiddlewareExecuted"] = true;
        context.Items["AsyncMiddlewareTimestamp"] = DateTime.UtcNow;

        // Continue to the next middleware/function
        await next(context);

        // Simulate async post-processing
        await Task.Delay(1);
    }
}