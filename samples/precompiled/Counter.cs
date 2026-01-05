// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates the Aggregator (Entity) pattern using Durable Entities.
 * Entities are stateful actors that can be created, updated, and queried.
 * Unlike orchestrations, entities have stable identities and their state persists
 * indefinitely until explicitly deleted.
 *
 * Pattern documentation:
 * https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-entities
 *
 * To run this sample, use the built-in entity webhooks (no client function needed):
 *   1. Start the function app locally using `func host start` or run from Visual Studio
 *   2. Reset the counter:
 *      curl -X POST -H "Content-Length: 0" "http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Reset"
 *   3. Add to the counter:
 *      curl -d "1" -X POST -H "Content-Type: application/json" http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Add
 *      curl -d "2" -X POST -H "Content-Type: application/json" http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Add
 *   4. Read the counter value:
 *      curl http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter
 *
 * The result of the final GET operation should be: {"value":3}
 *
 * No special app settings are required for this sample.
 */
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;

namespace VSSample
{
    // Entity class that maintains a counter value with Add, Reset, and Get operations.
    // The class-based syntax provides a cleaner way to define entity behavior.
    public class Counter
    {
        // The current counter value - persisted as entity state
        [JsonProperty("value")]
        public int CurrentValue { get; set; }

        // Operation: Add an amount to the current counter value
        public void Add(int amount) => this.CurrentValue += amount;
        
        // Operation: Reset the counter to zero
        public void Reset() => this.CurrentValue = 0;
        
        // Operation: Get the current counter value
        public int Get() => this.CurrentValue;

        // Entity function entry point that dispatches operations to the Counter class
        [FunctionName(nameof(Counter))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<Counter>();
    }
}