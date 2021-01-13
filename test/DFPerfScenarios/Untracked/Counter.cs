// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DFPerfScenarios
{
    public static class CounterTest
    {
        [FunctionName("StartCounter")]
        public static async Task<HttpResponseMessage> Start(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartCounter")] HttpRequestMessage req,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            Input input = await req.Content.ReadAsAsync<Input>();
            if (input == null || input.EventCount <= 0)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Please send a JSON payload formatted like {\"EventCount\": X} where X is a positive integer.");
            }

            string entityKey = DateTime.UtcNow.ToString("yyyyMMdd-hhmmss") + "-" + Guid.NewGuid().ToString("N");
            var entityId = new EntityId("Counter", entityKey);

            log.LogInformation($"Sending {input.EventCount} messages...");
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 200
            };

            Parallel.For(0, input.EventCount, parallelOptions, delegate (int i)
            {
                starter.SignalEntityAsync(entityId, "add", 1).GetAwaiter().GetResult();
            });

            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        private class Input
        {
            public int EventCount { get; set; }

            public int Instances { get; set; } = 1;
        }
    }

    public class Counter
    {
        [JsonProperty("value")]
        public int CurrentValue { get; set; }

        public void Add(int amount) => this.CurrentValue += amount;

        public void Reset() => this.CurrentValue = 0;

        public int Get() => this.CurrentValue;

        [FunctionName(nameof(Counter))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<Counter>();
    }
}
