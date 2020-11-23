// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
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
    public class EternalOrchestrations
    {
        private readonly TelemetryClient telemetryClient;

        public EternalOrchestrations(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        [FunctionName(nameof(Periodic_Cleanup_Loop))]
        public async Task Periodic_Cleanup_Loop(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var expire = context.GetInput<Expire>();
            await context.CallActivityAsync(nameof(this.CleanUpNotification), "CleanUp!");
            DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(10);
            if (expire.Value > nextCleanup)
            {
                await context.CreateTimer(nextCleanup, CancellationToken.None);
                context.ContinueAsNew(expire);
            }
        }

        [FunctionName(nameof(CleanUpNotification))]
        public void CleanUpNotification(
            [ActivityTrigger] string message)
        {
            // You can use TrackTrace to send a custom correlated trace message to Application Insights
            this.telemetryClient.TrackTrace(message);
        }

        [FunctionName(nameof(HttpStart_ExternalOrchestrations))]
        public async Task<IActionResult> HttpStart_ExternalOrchestrations(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            DateTime expireTime = DateTime.UtcNow.AddSeconds(60);
            string instanceId = await starter.StartNewAsync(nameof(this.Periodic_Cleanup_Loop), new Expire { Value = expireTime });

            log.LogInformation($"Started orchestration with ID = '{instanceId}'. expireTime is {expireTime}");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public class Expire
        {
            public DateTime Value { get; set; }
        }
    }
}
