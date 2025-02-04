// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using DTC = Microsoft.DurableTask.Client;

namespace Microsoft.Azure.Durable.Tests.E2E;
public static class OrchestrationQuery
{
    [Function(nameof(GetAllStatus))]
    public static async Task<HttpResponseData> GetAllStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DTC.DurableTaskClient client)
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
    
    [Function(nameof(GetRunningStatus))]
    public static async Task<HttpResponseData> GetRunningStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DTC.DurableTaskClient client)
    {
        try 
        {
            DTC.OrchestrationQuery filter = new DTC.OrchestrationQuery(Statuses: new List<OrchestrationRuntimeStatus> { OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Suspended });
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
