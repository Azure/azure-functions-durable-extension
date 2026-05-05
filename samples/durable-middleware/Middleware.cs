// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace DurableMiddlewareSample;

public sealed class LoggingOrchestrationMiddleware : ITaskOrchestrationMiddleware
{
    public async Task InvokeAsync(
        TaskOrchestrationMiddlewareContext context,
        TaskOrchestrationMiddlewareDelegate next)
    {
        ILogger logger = context.OrchestrationContext.CreateReplaySafeLogger<LoggingOrchestrationMiddleware>();
        FunctionContext? functionContext = context.GetFunctionContext();

        if (!context.IsReplaying)
        {
            logger.LogInformation(
                "Starting orchestration {Name} ({InstanceId}) from function {FunctionName} with input {Input}.",
                context.Name,
                context.InstanceId,
                functionContext?.FunctionDefinition.Name,
                context.Input);
        }

        await next(context);

        if (!context.IsReplaying)
        {
            logger.LogInformation(
                "Completed orchestration {Name} ({InstanceId}) with result {Result}.",
                context.Name,
                context.InstanceId,
                context.Result);
        }
    }
}

public sealed class LoggingActivityMiddleware(ILogger<LoggingActivityMiddleware> logger) : ITaskActivityMiddleware
{
    public async Task InvokeAsync(TaskActivityMiddlewareContext context, TaskActivityMiddlewareDelegate next)
    {
        FunctionContext? functionContext = context.GetFunctionContext();
        logger.LogInformation(
            "Starting activity {Name} for instance {InstanceId} from function {FunctionName} with input {Input}.",
            context.Name,
            context.InstanceId,
            functionContext?.FunctionDefinition.Name,
            context.Input);

        await next(context);

        logger.LogInformation(
            "Completed activity {Name} for instance {InstanceId} with result {Result}.",
            context.Name,
            context.InstanceId,
            context.Result);
    }
}