// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.DurableTaskEndToEndTests;

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

        public static async Task<string> AllOrchestratorActivityActions([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            EntityId input = ctx.GetInput<EntityId>();
            var stringInput = input.ToString();
            RetryOptions options = new RetryOptions(TimeSpan.FromSeconds(5), 3);

            await ctx.CreateTimer(ctx.CurrentUtcDateTime, CancellationToken.None);
            await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), stringInput);
            await ctx.CallActivityWithRetryAsync<string>(nameof(TestActivities.Hello), options, stringInput);
            await ctx.CallSubOrchestratorAsync<string>(nameof(TestOrchestrations.SayHelloInline), stringInput);
            await ctx.CallSubOrchestratorWithRetryAsync<string>(nameof(TestOrchestrations.SayHelloWithActivity), options, stringInput);
            ctx.StartNewOrchestration(nameof(TestOrchestrations.SayHelloWithActivityWithDeterministicGuid), stringInput);
            ctx.SignalEntity(input, "count");

            ctx.SetCustomStatus("AllAPICallsUsed");

            // next call shouldn't run because MaxOrchestrationCount is reached
            await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), stringInput);

            return "TestCompleted";
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

        public static async Task LockThenFailReplay([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var input = context.GetInput<(EntityId entityId, bool createNondeterminismFailure)>();

            await context.LockAsync(input.entityId);

            if (!(input.createNondeterminismFailure && context.IsReplaying))
            {
                await context.CallActivityAsync<string>(nameof(TestActivities.Hello), "Tokyo");
            }
        }

        public static async Task CreateEmptyEntities([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var entityIds = context.GetInput<EntityId[]>();
            var tasks = new List<Task>();
            for (int i = 0; i < entityIds.Length; i++)
            {
                tasks.Add(context.CallEntityAsync(entityIds[i], "delete"));
            }

            await Task.WhenAll(tasks);
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
                catch (ArgumentException)
                {
                    return "ArgumentException";
                }
            }
            else
            {
                eventValue = await ctx.WaitForExternalEvent("approval", timeout, defaultValue);
            }

            return eventValue;
        }

        public static async Task ApprovalWithCancellationToken([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            TimeSpan timeout = ctx.GetInput<TimeSpan>();

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Task waiterWithTimeout = ctx.WaitForExternalEvent("approvalNeverRaised", timeout, cts.Token);
                Task waiterWithoutTimeout = ctx.WaitForExternalEvent("approval");

                Task taskAwaited = await Task.WhenAny(waiterWithTimeout, waiterWithoutTimeout);
                if (taskAwaited == waiterWithoutTimeout)
                {
                    cts.Cancel();
                }
            }
        }

        public static async Task<bool> SimpleEventWithTimeoutSucceeds([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            TimeSpan timeout = ctx.GetInput<TimeSpan>();
            await ctx.WaitForExternalEvent<string>("approval", timeout);
            return true;
        }

        public static async Task<bool> SimpleActivityRetrySuccceds([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            (TimeSpan firstRetry, TimeSpan maxRetry) = ctx.GetInput<(TimeSpan, TimeSpan)>();

            RetryOptions retry = new RetryOptions(firstRetry, 5)
            {
                MaxRetryInterval = maxRetry,
            };

            await ctx.CallActivityWithRetryAsync<Guid>(nameof(TestActivities.NewGuid), retry, null);

            return true;
        }

        public static async Task<bool> SimpleTimerSucceeds([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            DateTime fireAt = ctx.GetInput<DateTime>();
            await ctx.CreateTimer(fireAt, CancellationToken.None);
            return true;
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

            RetryOptions options = new RetryOptions(TimeSpan.FromSeconds(2), 3);

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

            RetryOptions options = new RetryOptions(TimeSpan.FromSeconds(1), 3);

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

        public static async Task<DurableHttpResponse> CallHttpAsyncOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            TestDurableHttpRequest testRequest = ctx.GetInput<TestDurableHttpRequest>();
            DurableHttpRequest durableHttpRequest = ConvertTestRequestToDurableHttpRequest(testRequest);
            DurableHttpResponse response = await ctx.CallHttpAsync(durableHttpRequest);
            return response;
        }

        public static DurableHttpRequest ConvertTestRequestToDurableHttpRequest(TestDurableHttpRequest testRequest)
        {
            Dictionary<string, StringValues> testHeaders = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            if (testRequest.Headers != null)
            {
                foreach (KeyValuePair<string, string> header in testRequest.Headers)
                {
                    StringValues stringValues;
                    if (testHeaders.TryGetValue(header.Key, out stringValues))
                    {
                        stringValues = StringValues.Concat(stringValues, header.Value);
                        testHeaders[header.Key] = stringValues;
                    }
                    else
                    {
                        stringValues = new StringValues(header.Value);
                        testHeaders[header.Key] = stringValues;
                    }
                }
            }

            DurableHttpRequest durableHttpRequest = new DurableHttpRequest(
                method: testRequest.HttpMethod,
                uri: new Uri(testRequest.Uri),
                headers: testHeaders,
                content: testRequest.Content,
                tokenSource: testRequest.TokenSource,
                asynchronousPatternEnabled: testRequest.AsynchronousPatternEnabled,
                timeout: testRequest.Timeout);

            return durableHttpRequest;
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

        public static Task<ComplexType> ComplexTypeOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var input = context.GetInput<ComplexType>();
            var activityOutput = context.CallActivityAsync<ComplexType>(nameof(TestActivities.ComplexTypeActivity), input);
            return activityOutput;
        }

#pragma warning disable 618
        public static Task<string> LegacyOrchestration([OrchestrationTrigger] DurableOrchestrationContextBase ctx)
        {
            return ctx.CallActivityAsync<string>(nameof(TestActivities.LegacyActivity), null);
        }
#pragma warning restore 618

        public static async Task<string> SignalAndCallStringStore([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // construct entity id from entity name and a supplied GUID
            var entity = new EntityId("StringStore2", ctx.GetInput<Guid>().ToString());

            // signal and call (both of these will be delivered close together)
            ctx.SignalEntity(entity, "set", "333");

            var result = await ctx.CallEntityAsync<string>(entity, "get");

            if (result != "333")
            {
                return $"fail: wrong entity state: expected 333, got {result}";
            }

            // make another call to see if the state survives replay
            result = await ctx.CallEntityAsync<string>(entity, "get");

            if (result != "333")
            {
                return $"fail: wrong entity state: expected 333, got {result}";
            }

            return "ok";
        }

        public static async Task<string> EntityId_SignalAndCallStringStore([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entity = ctx.GetInput<EntityId>();

            ctx.SignalEntity(entity, "set", "333");

            var result = await ctx.CallEntityAsync<string>(entity, "get");

            if (result != "333")
            {
                return $"fail: wrong entity state: expected 333, got {result}";
            }

            return "ok";
        }

        public static async Task<string> StringStoreWithCreateDelete([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // construct entity id from entity name and a (deterministic) new guid key
            var entity = new EntityId("StringStore2", ctx.NewGuid().ToString());
            string result;

            // does not exist, so get should throw
            try
            {
                result = await ctx.CallEntityAsync<string>(entity, "get");
                return "fail: expected exception";
            }
            catch (InvalidOperationException)
            {
            }

            // still does not exist, so get should still throw
            try
            {
                await ctx.CallEntityAsync<string>(entity, "get");
                return "fail: expected exception";
            }
            catch (InvalidOperationException)
            {
            }

            await ctx.CallEntityAsync<string>(entity, "set", "aha");

            result = await ctx.CallEntityAsync<string>(entity, "get");

            if (result != "aha")
            {
                return $"fail: wrong entity state: expected aha, got {result}";
            }

            await ctx.CallEntityAsync<string>(entity, "delete");

            // no longer exists, so get should again throw
            try
            {
                await ctx.CallEntityAsync<string>(entity, "get");
                return "fail: expected exception";
            }
            catch (InvalidOperationException)
            {
            }

            // re-create the entity
            await ctx.CallEntityAsync<string>(entity, "set", "aha-aha");

            result = await ctx.CallEntityAsync<string>(entity, "get");

            if (result != "aha-aha")
            {
                return $"fail: wrong entity state: expected aha-aha, got {result}";
            }

            // finally delete it
            await ctx.CallEntityAsync<string>(entity, "delete");

            return "ok";
        }

        public static async Task<string> CallFaultyEntity([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var (entityId, rollbackOnException) = ctx.GetInput<(EntityId, bool)>();

            // construct entity id and proxy
            var entity = ctx.CreateEntityProxy<TestEntityClasses.IFaultyEntity>(entityId);
            bool isClassBased = entityId.EntityName == "ClassBasedFaultyEntity".ToLowerInvariant();
            async Task ExpectException(Task t)
            {
                try
                {
                    await t;
                    Assert.True(false, "expected exception");
                }
                catch (TargetInvocationException) when (isClassBased)
                {
                    // thrown by v2 when using DispatchAsync()
                }
                catch (TestEntityClasses.FaultyEntity.SerializableKaboom) when (!isClassBased)
                {
                    // thrown by v2 when not using DispatchAsync()
                }
                catch (FunctionFailedException)
                {
                    // thrown by v1
                }
                catch (Exception e)
                {
                    Assert.True(false, $"wrong exception: {e}");
                }
            }

            try
            {
                Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));

                await Assert.ThrowsAsync<EntitySchedulerException>(() => entity.SetToUnserializable());

                Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));

                await entity.SetToUnDeserializable();

                Assert.True(await ctx.CallEntityAsync<bool>(entityId, "exists"));

                await Assert.ThrowsAsync<EntitySchedulerException>(() => entity.Get());

                await ctx.CallEntityAsync<bool>(entityId, "deletewithoutreading");

                Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));

                await entity.Set(3);

                Assert.Equal(3, await entity.Get());

                await ExpectException(entity.SetThenThrow(333));

                if (rollbackOnException)
                {
                    Assert.Equal(3, await entity.Get());
                }
                else
                {
                    Assert.Equal(333, await entity.Get());
                }

                await ExpectException(entity.DeleteThenThrow());

                if (rollbackOnException)
                {
                    Assert.Equal(3, await entity.Get());
                }
                else
                {
                    Assert.Equal(0, await entity.Get());
                }

                await entity.Delete();

                Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));

                await ExpectException(entity.SetThenThrow(333));

                if (rollbackOnException)
                {
                    Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));
                }
                else
                {
                    Assert.True(await ctx.CallEntityAsync<bool>(entityId, "exists"));
                }

                // send batch
                ctx.SignalEntity(entityId, "Set", 42);
                ctx.SignalEntity(entityId, "SetThenThrow", 333);
                ctx.SignalEntity(entityId, "DeleteThenThrow");

                if (rollbackOnException)
                {
                    Assert.Equal(42, await entity.Get());
                }
                else
                {
                    Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));
                    ctx.SignalEntity(entityId, "Set", 42);
                }

                // send batch
                ctx.SignalEntity(entityId, "Get");
                ctx.SignalEntity(entityId, "Set", 42);
                ctx.SignalEntity(entityId, "Delete");
                ctx.SignalEntity(entityId, "Set", 43);
                ctx.SignalEntity(entityId, "DeleteThenThrow");

                if (rollbackOnException)
                {
                    Assert.Equal(43, await entity.Get());
                }
                else
                {
                    Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));
                }

                // send batch
                ctx.SignalEntity(entityId, "Set", 55);
                ctx.SignalEntity(entityId, "SetToUnserializable");

                if (rollbackOnException)
                {
                    Assert.Equal(55, await entity.Get());
                }
                else
                {
                    await Assert.ThrowsAsync<EntitySchedulerException>(() => entity.Get());
                    await ctx.CallEntityAsync<bool>(entityId, "deletewithoutreading");
                }

                // send batch
                ctx.SignalEntity(entityId, "Set", 56);
                ctx.SignalEntity(entityId, "SetToUnDeserializable");
                ctx.SignalEntity(entityId, "Set", 12);
                ctx.SignalEntity(entityId, "SetThenThrow", 999);

                if (rollbackOnException)
                {
                    // we rolled back to an un-deserializable state
                    await Assert.ThrowsAsync<EntitySchedulerException>(() => entity.Get());
                }
                else
                {
                    Assert.Equal(999, await entity.Get());
                }

                await ctx.CallEntityAsync<bool>(entityId, "deletewithoutreading");

                ctx.SignalEntity(entityId, "Set", 1);
                ctx.SignalEntity(entityId, "Delete");
                ctx.SignalEntity(entityId, "Set", 2);
                ctx.SignalEntity(entityId, "Delete");
                ctx.SignalEntity(entityId, "SetThenThrow", 3);

                if (rollbackOnException)
                {
                    Assert.False(await ctx.CallEntityAsync<bool>(entityId, "exists"));
                }
                else
                {
                    Assert.Equal(3, await entity.Get()); // not rolled back
                }

                return "ok";
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public static async Task<string> RollbackSignalsOnExceptions([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // construct entity id and proxy
            var entityId = ctx.GetInput<EntityId>();
            var entity = ctx.CreateEntityProxy<TestEntityClasses.IFaultyEntity>(entityId);

            // the receiver gets the signals sent from the entity with the entity current state
            EntityId receiver = new EntityId(nameof(TestEntities.SchedulerEntity), entityId.EntityKey);

            ctx.SignalEntity(entityId, "Set", 56);
            ctx.SignalEntity(entityId, "Send", receiver); // 1:56
            ctx.SignalEntity(entityId, "Set", 100);
            ctx.SignalEntity(entityId, "Send", receiver); // 2:100
            ctx.SignalEntity(entityId, "SendThenThrow", receiver);
            ctx.SignalEntity(entityId, "Send", receiver); // 3:100
            ctx.SignalEntity(entityId, "Set", 10);
            ctx.SignalEntity(entityId, "SendThenThrow", receiver);
            ctx.SignalEntity(entityId, "Send", receiver); // 4:10
            ctx.SignalEntity(entityId, "SetThenThrow", 66);
            ctx.SignalEntity(entityId, "Send", receiver); // 5:10
            ctx.SignalEntity(entityId, "SendThenMakeUnserializable", receiver);
            ctx.SignalEntity(entityId, "Send", receiver); // 6:10
            ctx.SignalEntity(entityId, "Set", 11);
            ctx.SignalEntity(entityId, "Send", receiver); // 7:11

            Assert.Equal(7, await entity.GetNumberIncrementsSent()); // this was the total number of sends
            Assert.Equal(11, await entity.Get()); // this was the last value written

            return "ok";
        }

        public static async Task<string> HandleUncallableFunctions([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entityId = ctx.GetInput<EntityId>();

            // even though the entity function cannot be called the entity can still be used as a lock!
            using (await ctx.LockAsync(entityId))
            {
                try
                {
                    var result = await ctx.CallEntityAsync<int>(entityId, "get");
                }
#if FUNCTIONS_V1
                catch (Exception e) when (e.Message == "Exception has been thrown by the target of an invocation.")
#else
                catch (Exception e) when (e.Message == "Exception of type 'System.Exception' was thrown.")
#endif
                {
                }
            }

            // signals fail silently
            ctx.SignalEntity(entityId, "hello");

            // test that the lock is still available
            using (await ctx.LockAsync(entityId))
            {
            }

            // try an uncallable activity
            try
            {
                var result = await ctx.CallActivityAsync<int>(nameof(UnconstructibleClass.UncallableActivity), null);
            }
#if FUNCTIONS_V1
            catch (FunctionFailedException e) when (e.Message == "The activity function 'UncallableActivity' failed: \"Exception has been thrown by the target of an invocation.\". See the function execution logs for additional details.")
#else
            catch (FunctionFailedException e) when (e.Message == "The activity function 'UncallableActivity' failed: \"Exception of type 'System.Exception' was thrown.\". See the function execution logs for additional details.")
#endif
            {
            }

            return "ok";
        }

        public static async Task<string> PollCounterEntity([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // get the id of the two entities used by this test
            var entityId = ctx.GetInput<EntityId>();

            while (true)
            {
                var result = await ctx.CallEntityAsync<int>(entityId, "get");

                if (result != 0)
                {
                    if (result == 1)
                    {
                        return "ok";
                    }
                    else
                    {
                        return $"fail: wrong entity state: expected 1, got {result}";
                    }
                }

                await ctx.CreateTimer(DateTime.UtcNow + TimeSpan.FromSeconds(1), CancellationToken.None);
            }
        }

        public static Task<string> FireAndForgetHelloOrchestration([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var id = ctx.StartNewOrchestration(nameof(TestOrchestrations.SayHelloWithActivity), "Heloise");
            return Task.FromResult(id);
        }

        public static async Task<string> LaunchOrchestrationFromEntity([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entityId = new EntityId("Launcher", ctx.NewGuid().ToString());

            await ctx.CallEntityAsync(entityId, "launch", "hello");

            while (true)
            {
                var orchestrationId = await ctx.CallEntityAsync<string>(entityId, "get");

                if (orchestrationId != null)
                {
                    return orchestrationId;
                }

                await ctx.CreateTimer(DateTime.UtcNow + TimeSpan.FromSeconds(1), CancellationToken.None);
            }
        }

        public static async Task DelayedSignal([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entityId = ctx.GetInput<EntityId>();

            await ctx.CreateTimer(DateTime.UtcNow + TimeSpan.FromSeconds(.2), CancellationToken.None);

            ctx.SignalEntity(entityId, "done");
        }

        public static async Task<string> LargeEntity([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entityId = ctx.GetInput<EntityId>();

            string content = new string('.', 100000);
            await ctx.CallEntityAsync<int>(entityId, "set", content);

            var result = await ctx.CallEntityAsync<string>(entityId, "get");
            if (result != content)
            {
                return $"fail: wrong entity state";
            }

            return "ok";
        }

        public static async Task<string> EntityToAndFromBlob([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            // get the ids of the two entities used by this test
            var entityId = ctx.GetInput<EntityId>();

            // activation loads from blob, but the latter does not exist so it will be empty
            string result = await ctx.CallEntityAsync<string>(entityId, "get");
            if (result != "")
            {
                return $"fail: expected empty content, but got {result}";
            }

            const int sizeOfEachAppend = 10;
            const int numberOfAppends = 50;

            // let's send many signals to this entity to append characters
            for (int i = 0; i < numberOfAppends; i++)
            {
                ctx.SignalEntity(entityId, "append", new string('.', sizeOfEachAppend));
            }

            // then send a signal to deactivate
            ctx.SignalEntity(entityId, "deactivate");

            // now try again to read the entity state - it should come back from storage intact
            result = await ctx.CallEntityAsync<string>(entityId, "get");
            var numberDotsExpected = numberOfAppends * sizeOfEachAppend;
            if (result != new string('.', numberDotsExpected))
            {
                return $"fail: expected {numberDotsExpected} dots, but the result (length {result.Length}) is different";
            }

            return "ok";
        }

        public static async Task<int> LockedBlobIncrement([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entityPlayingTheRoleOfASimpleLock = ctx.GetInput<EntityId>();
            int result;

            if (ctx.IsLocked(out _))
            {
                throw new Exception("test failed: lock context is incorrect");
            }

            using (await ctx.LockAsync(entityPlayingTheRoleOfASimpleLock))
            {
                if (!ctx.IsLocked(out var ownedLocks)
                    || ownedLocks.Count != 1
                    || !ownedLocks.First().Equals(entityPlayingTheRoleOfASimpleLock))
                {
                    throw new Exception("test failed: lock context is incorrect");
                }

                // read current value from blob
                var currentValue = await ctx.CallActivityAsync<string>(
                            nameof(TestActivities.LoadStringFromTextBlob),
                            entityPlayingTheRoleOfASimpleLock.EntityKey);

                // increment
                result = int.Parse(currentValue ?? "0") + 1;

                // write result to blob
                await ctx.CallActivityAsync(
                          nameof(TestActivities.WriteStringToTextBlob),
                          (entityPlayingTheRoleOfASimpleLock.EntityKey, result.ToString()));
            }

            if (ctx.IsLocked(out _))
            {
                throw new Exception("test failed: lock context is incorrect");
            }

            return result;
        }

        public static async Task<(int, int)> LockedTransfer([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var (from, to) = ctx.GetInput<(EntityId, EntityId)>();

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
                var t1 = ctx.CallEntityAsync<int>(from, "get");
                var t2 = ctx.CallEntityAsync<int>(to, "get");
                fromBalance = await t1;
                toBalance = await t2;

                // modify
                fromBalance--;
                toBalance++;

                // write balances in parallel
                var t3 = ctx.CallEntityAsync(from, "set", fromBalance);
                var t4 = ctx.CallEntityAsync(to, "set", toBalance);
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
            var entity1 = new EntityId("CounterEntity", "1");
            var entity2 = new EntityId("CounterEntity", "2");

            using (await ctx.LockAsync(entity1, entity2))
            {
                await Task.WhenAll(
                    ctx.CallEntityAsync(entity1, "add", 42),
                    ctx.CallEntityAsync(entity2, "add", -42));
            }
        }

        public static async Task SignalAndCallChatRoom([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entity = new EntityId("ChatRoom", "myChat");

            ctx.SignalEntity(entity, "Post", "Hello World");

            var result = await ctx.CallEntityAsync<List<KeyValuePair<DateTime, string>>>(entity, "Get");
        }

        public static async Task<string> BasicObjects([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var chatroom = ctx.GetInput<EntityId>();

            // signals
            ctx.SignalEntity(chatroom, "post", "a");
            ctx.SignalEntity(chatroom, "post", "b");

            // call returning a task, but no result
            await ctx.CallEntityAsync(chatroom, "post", "c");

            // calls returning a result
            var result = await ctx.CallEntityAsync<List<KeyValuePair<DateTime, string>>>(chatroom, "get");

            return string.Join(",", result.Select(kvp => kvp.Value));
        }

        public static async Task<string> ThreeSuccessiveCalls([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var (entityId, count) = ctx.GetInput<(EntityId, int)>();

            await ctx.CallEntityAsync(entityId, count.ToString());

            count++;

            if (count < 3)
            {
                ctx.ContinueAsNew((entityId, count));

                return "continue as new";
            }
            else
            {
                return "ok";
            }
        }

        public static async Task<string> ContinueAsNew_Repro285([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var input = ctx.GetInput<int>();

            if (input == 0)
            {
                ctx.ContinueAsNew(1);

                return "continue as new";
            }
            else if (input == 1)
            {
                await ctx.CreateTimer<object>(ctx.CurrentUtcDateTime + TimeSpan.FromSeconds(1), null, CancellationToken.None);
            }

            return "ok";
        }

        public static async Task<string> ContinueAsNewMultipleTimersAndEvents([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var input = ctx.GetInput<int>();

            if (input > 0)
            {
                var cts = new CancellationTokenSource();
                var timerTask = ctx.CreateTimer<object>(ctx.CurrentUtcDateTime + TimeSpan.FromDays(1), null, cts.Token);
                var eventTask = ctx.WaitForExternalEvent($"signal{input}");
                await Task.WhenAny(timerTask, eventTask);
                cts.Cancel();
                ctx.ContinueAsNew(input - 1);
                return input.ToString();
            }
            else if (input == 0)
            {
                return "ok";
            }
            else
            {
                return "error";
            }
        }

        public static async Task<bool> EntityProxyWithBindings([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var counter = ctx.GetInput<EntityId>();

            var entityProxy = ctx.CreateEntityProxy<TestEntityClasses.IAsyncCounter>(counter);

            // reset
            await entityProxy.Set(10);

            // increment
            await entityProxy.Increment();

            // add
            await entityProxy.Add(5);

            // get current value
            var result = await entityProxy.Get();

            // destruct
            entityProxy.Delete();

            return result == 16;
        }

        public static async Task<bool> EntityProxy([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var counter = ctx.GetInput<EntityId>();

            var entityProxy = ctx.CreateEntityProxy<TestEntityClasses.ICounter>(counter);

            // reset
            entityProxy.Set(10);

            // increment
            entityProxy.Increment();

            // add
            entityProxy.Add(5);

            // get current value
            var result = await entityProxy.Get();

            // destruct
            entityProxy.Delete();

            return result == 16;
        }

        public static async Task<bool> EntityProxy_MultipleInterfaces([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var job = ctx.GetInput<EntityId>();

            var jobEntity = ctx.CreateEntityProxy<TestEntityClasses.IJob>(job);
            var primaryJobEntity = ctx.CreateEntityProxy<TestEntityClasses.IPrimaryJob>(job);
            var date = DateTime.MinValue;

            // set start date
            jobEntity.SetStartDate(date);

            // set job id
            primaryJobEntity.SetId(ctx.InstanceId);

            // set end date
            jobEntity.SetEndDate(date.AddMinutes(10));

            var id = await primaryJobEntity.GetId();
            var duration = await jobEntity.GetDuration();

            // destruct
            primaryJobEntity.Delete();

            return id == ctx.InstanceId && duration == TimeSpan.FromMinutes(10);
        }

        public static async Task<bool> EntityProxy_NameResolve([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var entityKey = ctx.GetInput<string>();

            var entityProxy = ctx.CreateEntityProxy<TestEntityClasses.ICounter>(entityKey);

            // reset
            entityProxy.Set(10);

            // increment
            entityProxy.Increment();

            // add
            entityProxy.Add(5);

            // get current value
            var result = await entityProxy.Get();

            // destruct
            entityProxy.Delete();

            return result == 16;
        }

        public static async Task<string> ReplaySafeLogger_OneLogMessage([OrchestrationTrigger] IDurableOrchestrationContext ctx, ILogger log)
        {
            log = ctx.CreateReplaySafeLogger(log);
            string input = ctx.GetInput<string>();

            log.LogInformation("ReplaySafeLogger Test: About to say Hello");

            string output = await ctx.CallActivityAsync<string>(nameof(TestActivities.Hello), input);
            return output;
        }
    }
}
