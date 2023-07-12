using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace DFPerfScenarios.DFPerfScenarios
{
    public static class PurgeHistoryFunction
    {
        [FunctionName("PurgeHistory")]
        public static async Task<HttpResponseMessage> PurgeHistory(
            [HttpTrigger(AuthorizationLevel.Function, methods: "delete", Route = "PurgeHistory")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var queryParams = req.GetQueryNameValuePairs();

            var p = new Parameters
            {
                InstanceId = queryParams["InstanceId"],
                // TODO: Support overriding other values
            };

            Stopwatch sw = Stopwatch.StartNew();
            if (p.InstanceId != null)
            {
                await client.PurgeInstanceHistoryAsync(p.InstanceId);
            }
            else
            {
                await client.PurgeInstanceHistoryAsync(
                    p.CreatedTimeFrom ?? DateTime.MinValue,
                    p.CreatedTimeTo,
                    p.RuntimeStatus);
            }

            sw.Stop();

            string resultMessage = $"Purge completed in {sw.Elapsed}.";
            log.LogInformation(resultMessage);
            return req.CreateResponse(resultMessage);
        }

        class Parameters
        {
            public string InstanceId { get; set; }

            public DateTime? CreatedTimeFrom { get; set; }
            public DateTime? CreatedTimeTo { get; set; }
            public IEnumerable<OrchestrationStatus> RuntimeStatus { get; set; }
        }
    }
}
