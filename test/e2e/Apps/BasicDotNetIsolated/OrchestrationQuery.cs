// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace Microsoft.Azure.Durable.Tests.E2E;
public static class OrchestrationQueryFunctions
{
    [Function(nameof(GetAllInstances))]
    public static async Task<HttpResponseData> GetAllInstances(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        try 
        {
            var instances = client.GetAllInstancesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(instances);
            return response;
        }
        catch (RpcException ex) 
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "text/plain");
            await response.WriteStringAsync(ex.Message);
            return response;
        }
    }

    [Function(nameof(GetRunningInstances))]
    public static async Task<HttpResponseData> GetRunningInstances(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        try 
        {
            OrchestrationQuery filter = new OrchestrationQuery(Statuses: new List<OrchestrationRuntimeStatus> { 
                OrchestrationRuntimeStatus.Running, 
                OrchestrationRuntimeStatus.Pending, 
                OrchestrationRuntimeStatus.Suspended 
            });
            var instances = client.GetAllInstancesAsync(filter);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(instances);
            return response;
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
