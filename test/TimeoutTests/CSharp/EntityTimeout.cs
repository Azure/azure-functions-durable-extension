// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace TimeoutTests
{
    public static partial class TimeoutTests
    {
        [FunctionName(nameof(EntityTimeout1))]
        public static async Task<string> EntityTimeout1([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var entityId = new EntityId(nameof(SlowEntity), context.InstanceId);

            try
            {
                int result = await context.CallEntityAsync<int>(entityId, "Go", 180);
                return "Test failed: no exception thrown";
            }
            catch (Microsoft.Azure.WebJobs.Host.FunctionTimeoutException)
            {
                return "Test succeeded";
            }
            catch (Exception e)
            {
                return $"Test failed: wrong exception thrown: {e}";
            }
        }


        [FunctionName(nameof(EntityTimeout2))]
        public static async Task<string> EntityTimeout2([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var entityId = new EntityId(nameof(SlowEntity), context.InstanceId);

            context.SignalEntity(entityId, "Go", 0); // count = 1
            context.SignalEntity(entityId, "Go", 0); // count = 2
            context.SignalEntity(entityId, "Go", 3 * 60); // times out, so count is not incremented

            await context.CreateTimer(context.CurrentUtcDateTime + TimeSpan.FromMinutes(4), CancellationToken.None);

            int result = await context.CallEntityAsync<int>(entityId, "Go", 1); // count = 3

            if (result == 3)
            {
                return "Test succeeded";
            }
            else
            {
                return $"Test failed: expected=3, actual={result}";
            }
        }


        [FunctionName(nameof(EntityTimeout3))]
        public static async Task<string> EntityTimeout3([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var entityId = new EntityId(nameof(SlowEntity), context.InstanceId);

            context.SignalEntity(entityId, "Go", 0); // count = 1
            context.SignalEntity(entityId, "Go", 0); // count = 2
            context.SignalEntity(entityId, "Go", 3 * 60); // times out, so count is not incremented
            context.SignalEntity(entityId, "Go", 0); // count = 3

            int result = await context.CallEntityAsync<int>(entityId, "Go", 1); // count = 4

            if (result == 4)
            {
                return "Test succeeded";
            }
            else
            {
                return $"Test failed: expected=4, actual={result}";
            }
        }

        [FunctionName(nameof(EntityBatch1))]
        public static async Task<string> EntityBatch1([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var entityId = new EntityId(nameof(SlowEntity), context.InstanceId);

            // number of signals exceeds max batch size,
            // so history is saved as an intermediate step

            context.SignalEntity(entityId, "Go", 0);
            context.SignalEntity(entityId, "Go", 0);
            context.SignalEntity(entityId, "Go", 0);
            context.SignalEntity(entityId, "Go", 0);
            context.SignalEntity(entityId, "Go", 0);
            context.SignalEntity(entityId, "Go", 0);
            context.SignalEntity(entityId, "Go", 0);

            int result = await context.CallEntityAsync<int>(entityId, "Go", 0);

            if (result == 8)
            {
                return "Test succeeded";
            }
            else
            {
                return $"Test failed: expected=8, actual={result}";
            }
        }

        [FunctionName(nameof(EntityBatch2))]
        public static async Task<string> EntityBatch2([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var entityId = new EntityId(nameof(SlowEntity), context.InstanceId);

            // processing of first operation exceeds 1 minute,
            // so history is saved as an intermediate step

            context.SignalEntity(entityId, "Go", 70);
            context.SignalEntity(entityId, "Go", 0);

            int result = await context.CallEntityAsync<int>(entityId, "Go", 0); 

            if (result == 3)
            {
                return "Test succeeded";
            }
            else
            {
                return $"Test failed: expected=3, actual={result}";
            }
        }
    }


    public class SlowEntity
    {
        public int Count { get; set; }

        private ILogger logger;

        public SlowEntity(ILogger logger)
        {
            this.logger = logger;
        }

        public int Go(int seconds)
        {
            this.Count += 1;

            logger.LogWarning($"{Entity.Current.EntityId} Count={this.Count} duration={seconds}s");

            if (seconds > 0)
            {
                System.Threading.Thread.Sleep(seconds * 1000);
            }
           
            return this.Count;
        }

        [FunctionName(nameof(SlowEntity))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx, ILogger logger)
            => ctx.DispatchAsync<SlowEntity>(logger);
    }
}
