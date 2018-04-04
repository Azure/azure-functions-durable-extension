﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestOrchestrations
    {
        public static string SayHelloInline([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            return $"Hello, {input}!";
        }

        public static async Task<string> SayHelloWithActivity([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            string output = await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), input);
            return output;
        }

        public static async Task<long> Factorial([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            int n = ctx.GetInput<int>();

            long result = 1;
            for (int i = 1; i <= n; i++)
            {
                result = await ctx.CallActivityAsync<int>(nameof(TestActivities.Multiply), new[] { result, i });
            }

            return result;
        }

        public static async Task<long> DiskUsage([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string directory = ctx.GetInput<string>();
            string[] files = await ctx.CallActivityAsync<string[]>(nameof(TestActivities.GetFileList), directory);

            var tasks = new Task<long>[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                tasks[i] = ctx.CallActivityAsync<long>(nameof(TestActivities.GetFileSize), files[i]);
            }

            await Task.WhenAll(tasks);

            long totalBytes = tasks.Sum(t => t.Result);
            return totalBytes;
        }

        public static async Task<int> Counter([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            int currentValue = ctx.GetInput<int>();
            string operation = await ctx.WaitForExternalEvent<string>("operation");

            bool done = false;
            switch (operation?.ToLowerInvariant())
            {
                case "incr":
                    currentValue++;
                    break;
                case "decr":
                    currentValue--;
                    break;
                case "end":
                    done = true;
                    break;
            }

            if (!done)
            {
                ctx.ContinueAsNew(currentValue);
            }

            return currentValue;
        }

        public static async Task BatchActor([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            var requiredItems = new HashSet<string>(new[] { @"item1", @"item2", @"item3", @"item4", @"item5" });

            // If an item was sent in during StartAsNew() this handles that
            string itemName = ctx.GetInput<string>();
            requiredItems.Remove(itemName);

            while (requiredItems.Any())
            {
                itemName = await ctx.WaitForExternalEvent<string>("newItem");

                requiredItems.Remove(itemName);
            }

            // we've received events for all the required items; safe to bail now!
        }

        public static async Task<string> Approval([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            TimeSpan timeout = ctx.GetInput<TimeSpan>();
            DateTime deadline = ctx.CurrentUtcDateTime.Add(timeout);

            using (var cts = new CancellationTokenSource())
            {
                Task<bool> approvalTask = ctx.WaitForExternalEvent<bool>("approval");
                Task timeoutTask = ctx.CreateTimer(deadline, cts.Token);

                if (approvalTask == await Task.WhenAny(approvalTask, timeoutTask))
                {
                    // The timer must be cancelled or fired in order for the orchestration to complete.
                    cts.Cancel();

                    bool approved = approvalTask.Result;
                    return approved ? "Approved" : "Rejected";
                }
                else
                {
                    return "Expired";
                }
            }
        }

        public static async Task Throw([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();
            if (string.IsNullOrEmpty(message) || message.Contains("null"))
            {
                // This throw happens directly in the orchestration.
                throw new ArgumentNullException(nameof(message));
            }

            // This throw happens in the implementation of an activity.
            await ctx.CallActivityAsync(nameof(TestActivities.Throw), message);
        }

        public static async Task OrchestratorGreeting([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();

            await ctx.CallSubOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), message);
        }

        public static async Task OrchestratorThrowWithRetry([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();

            RetryOptions options = new RetryOptions(TimeSpan.FromSeconds(5), 3);

            // This throw happens in the implementation of an orchestrator.
            await ctx.CallSubOrchestratorWithRetryAsync(nameof(TestOrchestrations.Throw), options, message);
        }

        public static async Task OrchestratorWithRetry_NullRetryOptions([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();

            RetryOptions options = null;

            // This throw happens in the implementation of an orchestrator.
            await ctx.CallSubOrchestratorWithRetryAsync(nameof(TestOrchestrations.Throw), options, message);
        }

        public static async Task ActivityThrowWithRetry([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();
            if (string.IsNullOrEmpty(message))
            {
                // This throw happens directly in the orchestration.
                throw new ArgumentNullException(nameof(message));
            }

            RetryOptions options = new RetryOptions(TimeSpan.FromSeconds(5), 3);

            // This throw happens in the implementation of an activity.
            await ctx.CallActivityWithRetryAsync(nameof(TestActivities.Throw), options, message);
        }

        public static async Task ActivityWithRetry_NullRetryOptions([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();
            if (string.IsNullOrEmpty(message))
            {
                // This throw happens directly in the orchestration.
                throw new ArgumentNullException(nameof(message));
            }

            RetryOptions options = null;

            // This throw happens in the implementation of an activity.
            await ctx.CallActivityWithRetryAsync(nameof(TestActivities.Throw), options, message);
        }

        public static async Task<int> TryCatchLoop([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            int iterations = ctx.GetInput<int>();
            int catchCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    await ctx.CallActivityAsync(nameof(TestActivities.Throw), "Kah-BOOOOOM!!!");
                }
                catch (FunctionFailedException)
                {
                    catchCount++;
                }
            }

            return catchCount;
        }

        // TODO: It's not currently possible to detect this failure except by examining logs.
        public static async Task IllegalAwait([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            await ctx.CallActivityAsync(nameof(TestActivities.Hello), "Foo");

            // This is the illegal await
            await Task.Run(() => { });

            // This call should throw
            await ctx.CallActivityAsync(nameof(TestActivities.Hello), "Bar");
        }

        public static async Task<object> CallActivity([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
            var startArgs = ctx.GetInput<StartOrchestrationArgs>();
            var result = await ctx.CallActivityAsync<object>(startArgs.FunctionName, startArgs.Input);
            return result;
        }

        public static async Task<object> CallOrchestrator([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            // Using StartOrchestrationArgs to start an orchestrator function because it's easier than creating a new type.
            var startArgs = ctx.GetInput<StartOrchestrationArgs>();
            var result = await ctx.CallSubOrchestratorAsync<object>(
                startArgs.FunctionName,
                startArgs.InstanceId,
                startArgs.Input);
            return result;
        }

        public static async Task Timer([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            DateTime fireAt = ctx.GetInput<DateTime>();
            await ctx.CreateTimer(fireAt, CancellationToken.None);
        }

        public static string BigReturnValue([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            int stringLength = ctx.GetInput<int>();
            return new string('*', stringLength);
        }

        public static async Task SetStatus([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
            for (int i = 0; i < 3; i++)
            {
                object newStatus = await ctx.WaitForExternalEvent<object>("UpdateStatus");
                ctx.SetCustomStatus(newStatus);
            }

            // Make sure status updates can survive awaits
            await ctx.CreateTimer(ctx.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        public static async Task ParallelBatchActor([OrchestrationTrigger] DurableOrchestrationContext ctx)
        {
           Task item1 = ctx.WaitForExternalEvent<string>("newItem");
           Task item2 = ctx.WaitForExternalEvent<string>("newItem");
           Task item3 = ctx.WaitForExternalEvent<string>("newItem");
           Task item4 = ctx.WaitForExternalEvent<string>("newItem");
           await Task.WhenAll(item1, item2, item3, item4);
        }
    }
}
