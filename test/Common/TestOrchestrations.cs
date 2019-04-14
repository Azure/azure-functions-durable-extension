// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestOrchestrations
    {
        public const char BigValueChar = '*';

        public static bool SayHelloWithActivityForRewindShouldFail { get; set; } = true;

        public static string SayHelloInline([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            return $"Hello, {input}!";
        }

        public static async Task<string> SayHelloWithActivity([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            string output = await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), input);
            return output;
        }

        public static async Task<bool> SayHelloWithActivityWithDeterministicGuid([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            Guid firstGuid = ctx.NewGuid();
            Guid secondGuid = ctx.NewGuid();
            string output = await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), input);
            Guid thirdGuid = ctx.NewGuid();
            return firstGuid != secondGuid && firstGuid != thirdGuid && secondGuid != thirdGuid;
        }

        public static bool VerifyUniqueGuids([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            HashSet<Guid> guids = new HashSet<Guid>();
            for (int i = 0; i < 10000; i++)
            {
                Guid newGuid = ctx.NewGuid();
                if (guids.Contains(newGuid))
                {
                    return false;
                }
                else
                {
                    guids.Add(newGuid);
                }
            }

            return true;
        }

        public static async Task<bool> VerifySameGuidGeneratedOnReplay([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            Guid firstGuid = ctx.NewGuid();
            Guid firstOutputGuid = await ctx.CallActivityAsync<Guid>(nameof(TestActivities.Echo), firstGuid);
            if (firstGuid != firstOutputGuid)
            {
                return false;
            }

            Guid secondGuid = ctx.NewGuid();
            Guid secondOutputGuid = await ctx.CallActivityAsync<Guid>(nameof(TestActivities.Echo), secondGuid);
            if (secondGuid != secondOutputGuid)
            {
                return false;
            }

            return true;
        }

        public static async Task<string> EchoWithActivity([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            string output = await ctx.CallActivityAsync<string>(nameof(TestActivities.Echo), input);
            return output;
        }

        public static async Task<string> SayHelloWithActivityForRewind([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            string output = await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), input);
            if (SayHelloWithActivityForRewindShouldFail)
            {
                SayHelloWithActivityForRewindShouldFail = false;
                throw new Exception("Simulating Orchestration failure...");
            }

            return output;
        }

        public static async Task<string> SayHelloWithActivityAndCustomStatus([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string input = ctx.GetInput<string>();
            var customStatus = new { nextActions = new[] { "A", "B", "C" }, foo = 2, };
            ctx.SetCustomStatus(customStatus);
            string output = await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), input);
            return output;
        }

        public static async Task<long> Factorial([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            int n = ctx.GetInput<int>();

            long result = 1;
            for (int i = 1; i <= n; i++)
            {
                result = await ctx.CallActivityAsync<int>(nameof(TestActivities.Multiply), (result, i));
            }

            return result;
        }

        public static async Task<long> DiskUsage([OrchestrationTrigger] IDurableOrchestrationContext ctx)
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

        public static async Task<int> Counter([OrchestrationTrigger] IDurableOrchestrationContext ctx)
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

            // Allow clients to track the current value.
            ctx.SetCustomStatus(currentValue);

            if (!done)
            {
                ctx.ContinueAsNew(currentValue, preserveUnprocessedEvents: true);
            }

            return currentValue;
        }

        public static async Task BatchActor([OrchestrationTrigger] IDurableOrchestrationContext ctx)
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

        public static async Task BatchActorRemoveLast([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var requiredItems = new HashSet<string>(new[] { @"item1", @"item2", @"item3", @"item4", @"item5" });

            // If an item was sent in during StartAsNew() this handles that
            var itemName = ctx.GetInput<string>();
            requiredItems.Remove(itemName);

            while (requiredItems.Any())
            {
                await ctx.WaitForExternalEvent("deleteItem");

                requiredItems.Remove(requiredItems.Last());
            }

            // we've received events for all the required items; safe to bail now!
        }

        public static async Task<string> Approval([OrchestrationTrigger] IDurableOrchestrationContext ctx)
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

        public static async Task<string> ApprovalWithTimeout([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            (TimeSpan timeout, string defaultValue) = ctx.GetInput<(TimeSpan, string)>();
            DateTime deadline = ctx.CurrentUtcDateTime.Add(timeout);
            string eventValue;
            if (defaultValue == "throw")
            {
                try
                {
                    eventValue = await ctx.WaitForExternalEvent<string>("approval", timeout);
                }
                catch (TimeoutException)
                {
                    return "TimeoutException";
                }
            }
            else
            {
                eventValue = await ctx.WaitForExternalEvent("approval", timeout, defaultValue);
            }

            return eventValue;
        }

        public static async Task ThrowOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();
            if (string.IsNullOrEmpty(message) || message.Contains("null"))
            {
                // This throw happens directly in the orchestration.
                throw new ArgumentNullException(nameof(message));
            }

            // This throw happens in the implementation of an activity.
            await ctx.CallActivityAsync(nameof(TestActivities.ThrowActivity), message);
        }

        public static async Task OrchestratorGreeting([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();

            string childInstanceId = ctx.InstanceId + ":0";
            await ctx.CallSubOrchestratorAsync(
                nameof(TestOrchestrations.SayHelloWithActivity),
                childInstanceId,
                message);
        }

        public static async Task OrchestratorThrowWithRetry([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();

            RetryOptions options = new RetryOptions(TimeSpan.FromSeconds(5), 3);

            // Specify an explicit sub-orchestration ID that can be queried by the test driver.
            Guid subInstanceId = await ctx.CallActivityAsync<Guid>(nameof(TestActivities.NewGuid), null);
            ctx.SetCustomStatus(subInstanceId.ToString("N"));

            // This throw happens in the implementation of an orchestrator.
            await ctx.CallSubOrchestratorWithRetryAsync(
                nameof(TestOrchestrations.ThrowOrchestrator),
                options,
                subInstanceId.ToString("N"),
                message);
        }

        public static async Task OrchestratorWithRetry_NullRetryOptions([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();

            RetryOptions options = null;

            // This throw happens in the implementation of an orchestrator.
            await ctx.CallSubOrchestratorWithRetryAsync(nameof(TestOrchestrations.ThrowOrchestrator), options, message);
        }

        public static async Task ActivityThrowWithRetry([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();
            if (string.IsNullOrEmpty(message))
            {
                // This throw happens directly in the orchestration.
                throw new ArgumentNullException(nameof(message));
            }

            RetryOptions options = new RetryOptions(TimeSpan.FromSeconds(5), 3);

            // This throw happens in the implementation of an activity.
            await ctx.CallActivityWithRetryAsync(nameof(TestActivities.ThrowActivity), options, message);
        }

        public static async Task ActivityWithRetry_NullRetryOptions([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();
            if (string.IsNullOrEmpty(message))
            {
                // This throw happens directly in the orchestration.
                throw new ArgumentNullException(nameof(message));
            }

            RetryOptions options = null;

            // This throw happens in the implementation of an activity.
            await ctx.CallActivityWithRetryAsync(nameof(TestActivities.ThrowActivity), options, message);
        }

        public static async Task<int> TryCatchLoop([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            int iterations = ctx.GetInput<int>();
            int catchCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    await ctx.CallActivityAsync(nameof(TestActivities.ThrowActivity), "Kah-BOOOOOM!!!");
                }
                catch (FunctionFailedException)
                {
                    catchCount++;
                }
            }

            return catchCount;
        }

        public static async Task SubOrchestrationThrow([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string message = ctx.GetInput<string>();

            try
            {
                await ctx.CallSubOrchestratorAsync(nameof(TestOrchestrations.ThrowOrchestrator), message);
            }
            catch (FunctionFailedException e)
            {
                if (e.InnerException == null ||
                    e.GetBaseException().GetType() != typeof(InvalidOperationException) ||
                    !e.InnerException.Message.Contains(message))
                {
                    throw new Exception("InnerException was not the expected value.");
                }

                // rethrow the original exception
                throw;
            }
        }

        // TODO: It's not currently possible to detect this failure except by examining logs.
        public static async Task IllegalAwait([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            await ctx.CallActivityAsync(nameof(TestActivities.Hello), "Foo");

            // This is the illegal await
            await Task.Run(() => { });

            // This call should throw
            await ctx.CallActivityAsync(nameof(TestActivities.Hello), "Bar");
        }

        public static async Task<object> CallActivity([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
            var startArgs = ctx.GetInput<StartOrchestrationArgs>();
            var result = await ctx.CallActivityAsync<object>(startArgs.FunctionName, startArgs.Input);
            return result;
        }

        public static string ProvideParentInstanceId([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            return ctx.ParentInstanceId;
        }

        public static async Task<object> CallOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // Using StartOrchestrationArgs to start an orchestrator function because it's easier than creating a new type.
            var startArgs = ctx.GetInput<StartOrchestrationArgs>();
            var result = await ctx.CallSubOrchestratorAsync<object>(
                startArgs.FunctionName,
                startArgs.InstanceId,
                startArgs.Input);
            return result;
        }

        public static async Task Timer([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            DateTime fireAt = ctx.GetInput<DateTime>();
            await ctx.CreateTimer(fireAt, CancellationToken.None);
        }

        public static string BigReturnValue([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            int stringLength = ctx.GetInput<int>();
            return new string(BigValueChar, stringLength);
        }

        public static async Task SetStatus([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            for (int i = 0; i < 3; i++)
            {
                object newStatus = await ctx.WaitForExternalEvent<object>("UpdateStatus");
                ctx.SetCustomStatus(newStatus);
            }

            // Make sure status updates can survive awaits
            await ctx.CreateTimer(ctx.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        public static async Task<DurableOrchestrationStatus> GetDurableOrchestrationStatus([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            DurableOrchestrationStatus durableOrchestrationStatus = ctx.GetInput<DurableOrchestrationStatus>();
            DurableOrchestrationStatus result = await ctx.CallActivityAsync<DurableOrchestrationStatus>(
                nameof(TestActivities.UpdateDurableOrchestrationStatus),
                durableOrchestrationStatus);
            return result;
        }

        public static async Task ParallelBatchActor([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
           Task item1 = ctx.WaitForExternalEvent<string>("newItem");
           Task item2 = ctx.WaitForExternalEvent<string>("newItem");
           Task item3 = ctx.WaitForExternalEvent<string>("newItem");
           Task item4 = ctx.WaitForExternalEvent<string>("newItem");
           await Task.WhenAll(item1, item2, item3, item4);
        }

        public static async Task<int> Counter2([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            int value = 0;
            while (value < 100)
            {
                Task incr = ctx.WaitForExternalEvent<object>("incr");
                Task done = ctx.WaitForExternalEvent<object>("done");
                Task winner = await Task.WhenAny(incr, done);
                if (winner == incr)
                {
                    value++;
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        public static async Task<HttpManagementPayload> ReturnHttpManagementPayload(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            HttpManagementPayload activityPassedHttpManagementPayload =
                await ctx.CallActivityAsync<HttpManagementPayload>(nameof(TestActivities.GetAndReturnHttpManagementPayload), null);
            return activityPassedHttpManagementPayload;
        }

        public static async Task<string> FanOutFanIn(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            int parallelTasks = context.GetInput<int>();
            var tasks = new Task[parallelTasks];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = context.CallActivityAsync<string>(nameof(TestActivities.Hello), i.ToString("000"));
            }

            await Task.WhenAll(tasks);

            return "Done";
        }

        public static async Task<int> WaitForEventAndCallActivity(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            int sum = 0;

            // Sums all of the inputs and intermediate sums
            // If the 4 inputs are 0-4, then the calls are as following:
            // 0 + (0 + 0) = 0
            // 0 + (0 + 1) = 1
            // 1 + (1 + 2) = 4
            // 4 + (4 + 3) = 11
            // 11 + (11 + 4) = 26
            for (int i = 0; i < 5; i++)
            {
                int number = await context.WaitForExternalEvent<int>("add");
                sum += await context.CallActivityAsync<int>("Add", (sum, number));
            }

            return sum;
        }

        public static async Task<string> SignalAndCallStringStore([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // construct actor reference from actor name and a supplied GUID
            var actor = new ActorId("StringStore2", ctx.GetInput<Guid>().ToString());

            // signal and call (both of these will be delivered close together)
            ctx.SignalActor(actor, "set", "333");

            var result = await ctx.CallActorAsync<string>(actor, "get");

            if (result != "333")
            {
                return $"fail: wrong actor state: expected 333, got {result}";
            }

            // make another call to see if the state survives replay
            result = await ctx.CallActorAsync<string>(actor, "get");

            if (result != "333")
            {
                return $"fail: wrong actor state: expected 333, got {result}";
            }

            return "ok";
        }

        public static async Task<string> StringStoreWithCreateDelete([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // construct actor reference from actor name and a (deterministic) new guid key
            var actor = new ActorId("StringStore2", ctx.NewGuid().ToString());
            string result;

            // does not exist, so get should throw
            try
            {
                result = await ctx.CallActorAsync<string>(actor, "get");
                return "fail: expected exception";
            }
            catch (InvalidOperationException)
            {
            }

            // still does not exist, so get should still throw
            try
            {
                await ctx.CallActorAsync<string>(actor, "get");
                return "fail: expected exception";
            }
            catch (InvalidOperationException)
            {
            }

            await ctx.CallActorAsync<string>(actor, "set", "aha");

            result = await ctx.CallActorAsync<string>(actor, "get");

            if (result != "aha")
            {
                return $"fail: wrong actor state: expected aha, got {result}";
            }

            await ctx.CallActorAsync<string>(actor, "delete");

            // no longer exists, so get should again throw
            try
            {
                await ctx.CallActorAsync<string>(actor, "get");
                return "fail: expected exception";
            }
            catch (InvalidOperationException)
            {
            }

            // re-create the actor
            await ctx.CallActorAsync<string>(actor, "set", "aha-aha");

            result = await ctx.CallActorAsync<string>(actor, "get");

            if (result != "aha-aha")
            {
                return $"fail: wrong actor state: expected aha-aha, got {result}";
            }

            // finally delete it
            await ctx.CallActorAsync<string>(actor, "delete");

            return "ok";
        }

        public static async Task<string> PollCounterActor([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // get the id of the two actors used by this test
            var actorId = ctx.GetInput<ActorId>();

            while (true)
            {
                var result = await ctx.CallActorAsync<int>(actorId, "get");

                if (result != 0)
                {
                    if (result == 1)
                    {
                        return "ok";
                    }
                    else
                    {
                        return $"fail: wrong actor state: expected 1, got {result}";
                    }
                }

                await ctx.CreateTimer(DateTime.UtcNow + TimeSpan.FromSeconds(1), CancellationToken.None);
            }
        }

        public static async Task<string> LargeActor([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var actorId = ctx.GetInput<ActorId>();

            string content = new string('.', 100000);
            await ctx.CallActorAsync<int>(actorId, "set", content);

            var result = await ctx.CallActorAsync<string>(actorId, "get");
            if (result != content)
            {
                return $"fail: wrong actor state";
            }

            return "ok";
        }

        public static async Task<string> ActorToAndFromBlob([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // get the ids of the two actors used by this test
            var actorId = ctx.GetInput<ActorId>();

            // activation loads from blob, but the latter does not exist so it will be empty
            string result = await ctx.CallActorAsync<string>(actorId, "get");
            if (result != "")
            {
                return $"fail: expected empty content, but got {result}";
            }

            const int sizeOfEachAppend = 10;
            const int numberOfAppends = 50;

            // let's send many signals to this actor to append characters
            for (int i = 0; i < numberOfAppends; i++)
            {
                ctx.SignalActor(actorId, "append", new string('.', sizeOfEachAppend));
            }

            // then send a signal to deactivate
            ctx.SignalActor(actorId, "deactivate");

            // now try again to read the actor state - it should come back from storage intact
            result = await ctx.CallActorAsync<string>(actorId, "get");
            var numberDotsExpected = numberOfAppends * sizeOfEachAppend;
            if (result != new string('.', numberDotsExpected))
            {
                return $"fail: expected {numberDotsExpected} dots, but the result (length {result.Length}) is different";
            }

            return "ok";
        }

        public static async Task<int> LockedBlobIncrement([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var actorPlayingTheRoleOfASimpleLock = ctx.GetInput<ActorId>();
            int result;

            if (ctx.IsLocked(out _))
            {
                throw new Exception("test failed: lock context is incorrect");
            }

            using (await ctx.LockAsync(actorPlayingTheRoleOfASimpleLock))
            {
                if (!ctx.IsLocked(out var ownedLocks)
                    || ownedLocks.Count != 1
                    || !ownedLocks.First().Equals(actorPlayingTheRoleOfASimpleLock))
                {
                    throw new Exception("test failed: lock context is incorrect");
                }

                // read current value from blob
                var currentValue = await ctx.CallActivityAsync<string>(
                            nameof(TestActivities.LoadStringFromTextBlob),
                            actorPlayingTheRoleOfASimpleLock.ActorKey);

                // increment
                result = int.Parse(currentValue ?? "0") + 1;

                // write result to blob
                await ctx.CallActivityAsync(
                          nameof(TestActivities.WriteStringToTextBlob),
                          (actorPlayingTheRoleOfASimpleLock.ActorKey, result.ToString()));
            }

            if (ctx.IsLocked(out _))
            {
                throw new Exception("test failed: lock context is incorrect");
            }

            return result;
        }

        public static async Task<(int, int)> LockedTransfer([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var (from, to) = ctx.GetInput<(ActorId, ActorId)>();

            if (from.Equals(to))
            {
                throw new ArgumentException("from and to must be distinct");
            }

            if (ctx.IsLocked(out _))
            {
                throw new Exception("test failed: lock context is incorrect");
            }

            int fromBalance;
            int toBalance;

            using (await ctx.LockAsync(from, to))
            {
                if (!ctx.IsLocked(out var ownedLocks)
                    || ownedLocks.Count != 2
                    || !ownedLocks.Contains(from)
                    || !ownedLocks.Contains(to))
                {
                    throw new Exception("test failed: lock context is incorrect");
                }

                // read balances in parallel
                var t1 = ctx.CallActorAsync<int>(from, "get");
                var t2 = ctx.CallActorAsync<int>(to, "get");
                fromBalance = await t1;
                toBalance = await t2;

                // modify
                fromBalance--;
                toBalance++;

                // write balances in parallel
                var t3 = ctx.CallActorAsync(from, "set", fromBalance);
                var t4 = ctx.CallActorAsync(to, "set", toBalance);
                await t3;
                await t4;
            }

            if (ctx.IsLocked(out _))
            {
                throw new Exception("test failed: lock context is incorrect");
            }

            return (fromBalance, toBalance);
        }

        public static async Task UpdateTwoCounters([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var actor1 = new ActorId("CounterActor", "1"); // construct actor reference from actor class and actor key
            var actor2 = new ActorId("CounterActor", "2"); // construct actor reference from actor class and actor key

            using (await ctx.LockAsync(actor1, actor2))
            {
                await Task.WhenAll(
                    ctx.CallActorAsync(actor1, "add", 42),
                    ctx.CallActorAsync(actor2, "add", -42));
            }
        }

        public static async Task SignalAndCallChatRoom([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var actor = new ActorId("ChatRoom", "myChat"); // construct actor reference from actor class and actor key

            ctx.SignalActor(actor, "Post", "Hello World");

            var result = await ctx.CallActorAsync<List<KeyValuePair<DateTime, string>>>(actor, "Get");
        }
    }
}
