// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E
{
    public static class PurgeOrchestrationHistory
    {
        [Function(nameof(PurgeOrchestrationHistory))]
        public static async Task<HttpResponseData> PurgeHistory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("HelloCities_HttpStart");

            logger.LogInformation("Starting purge all instance history");

            var requestPurgeResult = await client.PurgeAllInstancesAsync(new PurgeInstancesFilter(null, DateTime.UtcNow, new List<OrchestrationRuntimeStatus>{
                OrchestrationRuntimeStatus.Completed,
                OrchestrationRuntimeStatus.Failed,
                OrchestrationRuntimeStatus.Terminated
            }));

            logger.LogInformation("Finished purge all instance history");

            var response =  req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain");
            await response.WriteStringAsync($"Purged {requestPurgeResult.PurgedInstanceCount} records");
            return response;
        }
    }
}
