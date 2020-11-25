// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json.Linq;

namespace FunctionAppCorrelation
{
    /// <summary>
    /// This example is for testing that Durable Entities work with the new correlation implementation.
    /// </summary>
    public class EntityOrchestration
    {
        private const string CounterName = "myCounter";

        [FunctionName(nameof(IncrementOrchestration))]
        public async Task<int> IncrementOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var entityId = new EntityId(nameof(Counter), CounterName);
            await context.CallEntityAsync(entityId, "Add", 1);
            var counter = await context.CallEntityAsync<int>(entityId, "Get");
            counter++;
            return counter;
        }

        [FunctionName(nameof(HttpStart_EntityCounter))]
        public async Task<IActionResult> HttpStart_EntityCounter(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [DurableClient] IDurableEntityClient entityClient,
            [DurableClient] IDurableOrchestrationClient orchestrationClient)
        {
            var entityId = new EntityId(nameof(Counter), CounterName);
            EntityStateResponse<JObject> stateResponse = await entityClient.ReadEntityStateAsync<JObject>(entityId);

            await entityClient.SignalEntityAsync(entityId, "Add", 1);
            var instanceId = await orchestrationClient.StartNewAsync(nameof(this.IncrementOrchestration), null);

            return orchestrationClient.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
