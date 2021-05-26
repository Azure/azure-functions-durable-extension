using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
		public static IActionResult Start(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartCounter")] HttpRequest req,
            [DurableClient] IDurableClient starter,
            ILogger log)
		{
            if (!int.TryParse(req.Query["count"], out int count) || count < 1)
            {
                return new BadRequestObjectResult("A 'count' query string parameter is required and it must contain a positive number.");
            }

            var entityId = new EntityId("Counter", Guid.NewGuid().ToString("N"));

            log.LogInformation($"Sending {count} messages...");
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 200
            };

            Parallel.For(0, count, parallelOptions, delegate (int i)
            {
                starter.SignalEntityAsync(entityId, "add", 1).GetAwaiter().GetResult();
            });

            return new AcceptedResult();
		}

        private class Input
        {
            public int EventCount { get; set; } = 100;

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
