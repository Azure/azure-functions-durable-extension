// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;

namespace VSSample
{
    // This sample does not include any "client" functions. Instead, you can just use
    // the built-in webhooks to create and interact with this entity type.
    // 
    // Examples:
    //   curl -X POST -H "Content-Length: 0" "http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Reset"
    //   curl -d "1" -X POST -H "Content-Type: application/json" http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Add
    //   curl -d "2" -X POST -H "Content-Type: application/json" http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Add
    //   curl http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter
    //
    // The result of the final GET operation should be: {"value":3}
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