// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class SuspendResumeOrchestration
{
    [Function("SuspendInstance")]
    public static async Task<HttpResponseData> Suspend(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        string suspendReason = "Suspending the instance for test.";
        try 
        {
            await client.SuspendInstanceAsync(instanceId, suspendReason);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (RpcException ex) 
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "text/plain");
            await response.WriteStringAsync(ex.Message);
            return response;
        }
    }

    [Function("ResumeInstance")]
    public static async Task<HttpResponseData> Resume(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        string resumeReason = "Resuming the instance for test.";
        try 
        {
            await client.ResumeInstanceAsync(instanceId, resumeReason);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (RpcException ex) 
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "text/plain");
            await response.WriteStringAsync(ex.Message);
            return response;
        }
    }
}
