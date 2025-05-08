// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class ActivityErrorHandling
{
    private static ConcurrentDictionary<string, int> globalRetryCount = new ConcurrentDictionary<string, int>();

    [Function("RethrowActivityException_HttpStart")]
    public static async Task<HttpResponseData> RethrowHttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("RethrowActivityException_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RethrowActivityException));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("CatchActivityException_HttpStart")]
    public static async Task<HttpResponseData> CatchHttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("CatchActivityException_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(CatchActivityException));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("CatchActivityExceptionFailureDetails_HttpStart")]
    public static async Task<HttpResponseData> CatchFailureDetailsHttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("CatchActivityExceptionFailureDetails_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(CatchActivityExceptionFailureDetails));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("RetryActivityException_HttpStart")]
    public static async Task<HttpResponseData> RetryHttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("RetryActivityException_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RetryActivityFunction));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("CustomRetryActivityException_HttpStart")]
    public static async Task<HttpResponseData> CustomRetryHttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("CustomRetryActivityException_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(CustomRetryActivityFunction));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(RethrowActivityException))]
    public static async Task<string> RethrowActivityException(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var output = await context.CallActivityAsync<string>(nameof(RaiseException), context.InstanceId);
        return output;
    }

    [Function(nameof(CatchActivityException))]
    public static async Task<string> CatchActivityException(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        try 
        {
            var output = await context.CallActivityAsync<string>(nameof(RaiseException), context.InstanceId);
            return output;
        }
        catch (TaskFailedException ex)
        {  
            return ex.Message;
        }
    }

    [Function(nameof(CatchActivityExceptionFailureDetails))]
    public static async Task<TaskFailureDetails?> CatchActivityExceptionFailureDetails(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        try 
        {
            await context.CallActivityAsync<string>(nameof(RaiseException), context.InstanceId);
            return null;
        }
        catch (TaskFailedException ex)
        {      
            return ex.FailureDetails;
        }
    }

    [Function(nameof(RetryActivityFunction))]
    public static async Task<string> RetryActivityFunction(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var options = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(3)));

        var output = await context.CallActivityAsync<string>(nameof(RaiseException), context.InstanceId, options: options);
        return output;
    }

    [Function(nameof(CustomRetryActivityFunction))]
    public static async Task<string> CustomRetryActivityFunction(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var options = TaskOptions.FromRetryHandler(retryContext => {
            if (retryContext.LastFailure.IsCausedBy<InvalidOperationException>() && 
                    retryContext.LastFailure.InnerFailure is not null && 
                    retryContext.LastFailure.InnerFailure.IsCausedBy<OverflowException>() && 
                    retryContext.LastAttemptNumber < 3) {
                return true;
            }
            return false;
        });

        string output = await context.CallActivityAsync<string>(nameof(RaiseComplexException), context.InstanceId, options: options);
        return output;
    }

    [Function(nameof(RaiseException))]
    public static string RaiseException([ActivityTrigger] string instanceId, FunctionContext executionContext)
    {
        if (globalRetryCount.AddOrUpdate(instanceId, 1, (key, oldValue) => oldValue + 1) == 1)
        {
            throw new InvalidOperationException("This activity failed");
        }
        else
        {
            return "Success";
        }
    }

    [Function(nameof(RaiseComplexException))]
    public static string RaiseComplexException([ActivityTrigger] string instanceId, FunctionContext executionContext)
    {
        if (globalRetryCount.AddOrUpdate(instanceId, 1, (key, oldValue) => oldValue + 1) == 1)
        {
            var exception = new InvalidOperationException("This activity failed\r\nMore information about the failure", innerException: new OverflowException("Inner exception message"));
            throw exception;
        }
        else
        {
            return "Success";
        }
    }
}
