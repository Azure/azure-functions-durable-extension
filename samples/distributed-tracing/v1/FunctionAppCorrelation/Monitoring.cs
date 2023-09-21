// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class Monitoring
    {
        private readonly TelemetryClient telemetryClient;

        // The demo purpose implementation. For production, use Durable Entity to store the state.
        private static readonly ConcurrentDictionary<string, string> State = new ConcurrentDictionary<string, string>();

        public Monitoring(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        [FunctionName(nameof(MonitorJobStatus))]
        public async Task MonitorJobStatus(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var job = context.GetInput<Job>();
            int pollingInterval = 30;   // Second
            while (context.CurrentUtcDateTime < job.ExpiryTime)
            {
                var jobStatus = await context.CallActivityAsync<string>(nameof(this.GetJobStatus), job.JobId);
                if (jobStatus == "Completed")
                {
                    await context.CallActivityAsync(nameof(this.SendAlert), $"Job({job.JobId}) Completed.");
                    break;
                }

                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }
        }

        [FunctionName(nameof(SendAlert))]
        public void SendAlert(
            [ActivityTrigger] string message)
        {
            this.telemetryClient.TrackTrace(message);
        }

        [FunctionName(nameof(GetJobStatus))]
        public Task<string> GetJobStatus(
            [ActivityTrigger] IDurableActivityContext context)
        {
            var jobId = context.GetInput<string>();

            // The demo purpose implementation. For production, use Durable Entity to store the state.
            var status = State.AddOrUpdate(jobId, "Scheduled", (key, oldValue) =>
            {
                switch (oldValue)
                {
                    case "Scheduled":
                        return "Running";
                    case "Running":
                        return "Completed";
                    default:
                        return "Failed";
                }
            });
            return Task.FromResult(status);
        }

        [FunctionName(nameof(HttpStart_Monitor))]
        public async Task<IActionResult> HttpStart_Monitor(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string jobId = req.Query["JobId"];

            if (string.IsNullOrEmpty(jobId))
            {
                return new BadRequestObjectResult("Parameter JobId can not be null. Add '?JobId={someId}' on your request URI.");
            }

            var expirTime = DateTime.UtcNow.AddSeconds(180);

            string instanceId = await starter.StartNewAsync(nameof(this.MonitorJobStatus), new Job()
            {
                JobId = jobId,
                ExpiryTime = expirTime,
            });

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public class Job
        {
            public string JobId { get; set; }

            public DateTime ExpiryTime { get; set; }
        }
    }
}
