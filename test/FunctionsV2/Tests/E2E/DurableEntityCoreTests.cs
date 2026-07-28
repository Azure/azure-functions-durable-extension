// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class DurableEntityCoreTests : DurableTaskEndToEndTestBase
    {
        public DurableEntityCoreTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SignalAndCallStringStore(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SignalAndCallStringStore),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SignalAndCallStringStore),
                extendedSessions))
            {
                await host.StartAsync();

                var guid = Guid.NewGuid(); // used as the key for the entity

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], guid, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", (string)status.Output);

                // try to read the state of the entity directly from the client
                var response = await client.InnerClient.ReadEntityStateAsync<string>(new EntityId("StringStore2", guid.ToString()));
                Assert.True(response.EntityExists);
                Assert.Equal("333", response.EntityState);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple entity scenario involving creation and deletion.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_StringStoreWithCreateDelete(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.StringStoreWithCreateDelete),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_StringStoreWithCreateDelete),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "start", this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("start", status.Input);
                Assert.Equal("ok", status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates batching of entity signals.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_BatchedSignals(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_BatchedSignals),
                extendedSessions))
            {
                await host.StartAsync();

                int numIterations = 100;
                var entityId = new EntityId(nameof(TestEntities.BatchEntity), Guid.NewGuid().ToString());
                var client = await host.GetEntityClientAsync(entityId, this.output);

                // send a number of signals immediately after each other
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < numIterations; i++)
                {
                    tasks.Add(client.SignalEntity(this.output, i.ToString()));
                }

                await Task.WhenAll(tasks);

                var result = await client.WaitForEntityState<List<(int, int)>>(
                    this.output,
                    timeout: Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(20),
                    list => list.Count == numIterations ? null : $"waiting for {numIterations - list.Count} signals");

                // validate the batching positions and sizes
                int? cursize = null;
                int curpos = 0;
                int numBatches = 0;
                foreach (var (position, size) in result)
                {
                    if (cursize == null)
                    {
                        cursize = size;
                        curpos = 0;
                        numBatches++;
                    }

                    Assert.Equal(curpos, position);

                    if (++curpos == cursize)
                    {
                        cursize = null;
                    }
                }

                // there should always be some batching going on
                Assert.True(numBatches < numIterations);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates batching of entity signals.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_NonexistentEntity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.NonexistentEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_NonexistentEntity),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates exception handling in entity operations.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(false, false, false)]
        public async Task DurableEntity_CallFaultyEntity(bool extendedSessions, bool useClassBasedEntity, bool rollbackOnExceptions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallFaultyEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_CallFaultyEntity),
                extendedSessions,
                rollbackEntityOperationsOnExceptions: rollbackOnExceptions))
            {
                await host.StartAsync();
                var entityName = useClassBasedEntity ? "ClassBasedFaultyEntity" : "FunctionBasedFaultyEntity";
                var entityId = new EntityId(entityName, Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], (entityId, rollbackOnExceptions), this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates rollback of sent signals on exceptions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task DurableEntity_RollbackSignalsOnExceptions(bool extendedSessions, bool useClassBasedEntity)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_RollbackSignalsOnExceptions),
                extendedSessions,
                rollbackEntityOperationsOnExceptions: true))
            {
                await host.StartAsync();
                var entityName = useClassBasedEntity ? "ClassBasedFaultyEntity" : "FunctionBasedFaultyEntity";
                var entityKey = Guid.NewGuid().ToString();
                var entityId = new EntityId(entityName, entityKey);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.RollbackSignalsOnExceptions), entityId, this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output);

                var receiverEntityId = new EntityId(nameof(TestEntities.SchedulerEntity), entityKey);
                TestEntityClient receiverClient = await host.GetEntityClientAsync(receiverEntityId, this.output);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var state = await receiverClient.WaitForEntityState<List<string>>(this.output, timeout, curstate => curstate.Count >= 7 ? null : "expect 11 messages");
                Assert.Equal(new string[] { "1:56", "2:100", "3:100", "4:10", "5:10", "6:10", "7:11" }, state);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple entity scenario which sends a signal
        /// to a relay which forwards it to counter, and polls until the signal is delivered.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SignalThenPoll(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.PollCounterEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SignalThenPoll),
                extendedSessions))
            {
                await host.StartAsync();

                var relayEntityId = new EntityId("Relay", "");
                var counterEntityId = new EntityId("Counter", Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counterEntityId, this.output);

                await client.InnerClient.SignalEntityAsync(relayEntityId, "", (counterEntityId, "increment"));

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates launching orchestrations from entities.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityFireAndForget(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityFireAndForget),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.LaunchOrchestrationFromEntity),
                    null,
                    this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                var instanceId = (string)status.Output;
                Assert.NotNull(instanceId);
                var launchedStatus = await client.InnerClient.GetStatusAsync(instanceId, false, false, false);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, launchedStatus.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple entity scenario where an entity's state is
        /// larger than what fits into Azure table rows.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_LargeEntity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LargeEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_LargeEntity),
                extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId("StringStore2", Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityId, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output);

                var response = await client.InnerClient.ReadEntityStateAsync<string>(entityId);
                Assert.True(response.EntityExists);
                Assert.Equal(100000, response.EntityState.Length);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario involving a blob-backed entity that stores text and,
        /// when deactivated, saves its state to storage. The test concurrently runs an orchestration that
        /// creates a load of "append" operations, and sends periodic "deactivate" operations to the entity.
        /// At the end, it validates that all of the appends are reflected in the final state.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityToAndFromBlob(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityToAndFromBlob),
                extendedSessions))
            {
                await host.StartAsync();

                await EnsureBlobContainerExists("test");

                var entityId = new EntityId("BlobBackedTextStore", Guid.NewGuid().ToString());

                // first, start the orchestration
                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.EntityToAndFromBlob),
                    entityId,
                    this.output);

                DurableOrchestrationStatus status;
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 240);

                while (true)
                {
                    await Task.Delay(1000);

                    // while the orchestration is running, just for fun,
                    // send some deactivation signals which unload the entity from memory.
                    // this should not change the final outcome as the entities are storage-backed.
                    await client.InnerClient.SignalEntityAsync(entityId, "deactivate");

                    status = await client.GetStatusAsync();
                    if (status == null)
                    {
                        continue;
                    }

                    if (DateTime.UtcNow >= deadline ||
                        ((status.RuntimeStatus != OrchestrationRuntimeStatus.Pending)
                         && (status.RuntimeStatus != OrchestrationRuntimeStatus.Running)))
                    {
                        break;
                    }
                }

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", (string)status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Send a bunch of signals from a client to a single entity, then test that they are all being delivered.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, false, 1)]
        [InlineData(true, false, 2)]
        [InlineData(true, false, 20)]
        [InlineData(true, false, 200)]
        [InlineData(false, false, 1)]
        [InlineData(false, false, 2)]
        [InlineData(false, false, 20)]
        [InlineData(false, false, 200)]
        [InlineData(true, true, 1)]
        [InlineData(true, true, 2)]
        [InlineData(true, true, 20)]
        [InlineData(true, true, 200)]
        [InlineData(false, true, 1)]
        [InlineData(false, true, 2)]
        [InlineData(false, true, 20)]
        [InlineData(false, true, 200)]
        public async Task DurableEntity_ManyScheduledSignals(bool extendedSessions, bool delay, int numSignals)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_ManyScheduledSignals),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntities.SchedulerEntity), Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);

                var now = DateTime.UtcNow;

                for (int i = 0; i < numSignals; i++)
                {
                    if (delay)
                    {
                        await client.SignalEntity(this.output, now + TimeSpan.FromSeconds(i * (3.0 / numSignals)), i.ToString(), null);
                    }
                    else
                    {
                        await client.SignalEntity(this.output, i.ToString(), null);
                    }
                }

                string DescribeWhatsMissing(List<string> curstate)
                {
                    var expected = new HashSet<string>();
                    for (int i = 0; i < numSignals; i++)
                    {
                        expected.Add(i.ToString());
                    }

                    foreach (var s in curstate)
                    {
                        expected.Remove(s);
                    }

                    return expected.Count == 0 ? null : string.Join(",", expected);
                }

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var state = await client.WaitForEntityState<List<string>>(this.output, timeout, DescribeWhatsMissing);

                this.output.WriteLine(string.Join(", ", state));

                // The scheduled signals are not guaranteed to be delivered in order, so we sort before comparing
                var intlist = state.Select(s => int.Parse(s)).ToList();
                intlist.Sort();

                for (int i = 0; i < numSignals; i++)
                {
                    Assert.Equal(i, intlist[i]);
                }

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates calling an entity from successive incarnations of an orchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_ContinueAsNewBetweenCalls(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_ContinueAsNewBetweenCalls),
                extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntities.SchedulerEntity), Guid.NewGuid().ToString("N"));

                var orchestratorClient = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.ThreeSuccessiveCalls),
                    (entityId, 0),
                    this.output);

                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10);
                await client.WaitForEntityState<List<string>>(this.output, timeout, curstate => curstate.Count == 3 ? null : "expect 3 calls");

                var status = await orchestratorClient.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                Assert.Equal("ok", (string)status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Send a scheduled signal, then an immediate signal, and test delivery order.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task DurableEntity_ScheduledSignal(bool extendedSessions, bool useUtc)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_ScheduledSignal),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntities.SchedulerEntity), Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);

                // Wait for ownership lease acquisition.
                await Task.Delay(3000);

                var now = useUtc ? DateTime.UtcNow : DateTime.Now;

                await client.SignalEntity(this.output, now + TimeSpan.FromSeconds(20), "delayed", null);
                await client.SignalEntity(this.output, "immediate", null);

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(60);
                var state = await client.WaitForEntityState<List<string>>(this.output, timeout, curstate => curstate.Count == 2 ? null : "expect both messages");

                Assert.Equal("immediate, delayed", string.Join(", ", state));

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test an entity that signals itself with a delay.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SelfSchedulingEntity(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SelfSchedulingEntity),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntityClasses.SelfSchedulingEntity), Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);
                await client.SignalEntity(this.output, "Start", null);

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(60);
                var state = await client.WaitForEntityState<TestEntityClasses.SelfSchedulingEntity>(this.output, timeout, curstate => curstate.Value.Length == 4 ? null : "expect 4 letters");

                Assert.Equal("ABCD", state.Value);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario where three "LockedIncrement" orchestrations
        /// concurrently increment a counter saved in blob storage, using a read-modify-write pattern, while holding
        /// a lock on the same entity. This tests that the lock prevents the interleaving of these orchestrations.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_LockedIncrements(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LockedBlobIncrement),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_LockedIncrements),
                extendedSessions))
            {
                await host.StartAsync();

                await EnsureBlobContainerExists("test");

                var entityPlayingALock = new EntityId("Counter", Guid.NewGuid().ToString()); // does not matter what entity we use

                // start three concurrent increment operations
                // the lock should prevent incorrect interleavings

                var client1 = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityPlayingALock, this.output);
                var client2 = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityPlayingALock, this.output);
                var client3 = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityPlayingALock, this.output);

                var status1 = await client1.WaitForCompletionAsync(this.output);
                var status2 = await client2.WaitForCompletionAsync(this.output);
                var status3 = await client3.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status1?.RuntimeStatus);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status2?.RuntimeStatus);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status3?.RuntimeStatus);

                var result = new int[] { (int)status1?.Output, (int)status2?.Output, (int)status3?.Output };
                Array.Sort(result);

                for (int i = 0; i < result.Length; i++)
                {
                    Assert.True(result[i] == i + 1);
                }

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario where a "LockedTransfer" orchestration locks
        /// two "Counter" entities, and then in parallel increments/decrements them, respectively, using
        /// a read-modify-write pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SingleLockedTransfer(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SingleLockedTransfer),
                extendedSessions))
            {
                await host.StartAsync();

                var counter1 = new EntityId("Counter", Guid.NewGuid().ToString());
                var counter2 = new EntityId("Counter", Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.LockedTransfer),
                    (counter1, counter2),
                    this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                // validate the state of the counters
                var response1 = await client.InnerClient.ReadEntityStateAsync<int>(counter1);
                var response2 = await client.InnerClient.ReadEntityStateAsync<int>(counter2);
                Assert.True(response1.EntityExists);
                Assert.True(response2.EntityExists);
                Assert.Equal(-1, response1.EntityState);
                Assert.Equal(1, response2.EntityState);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario where a a number of "LockedTransfer" orchestrations
        /// concurrently operate on a number of entities, in a classical dining-philosophers configuration.
        /// This showcases the deadlock prevention mechanism achieved by the sequential, ordered lock acquisition.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, 5)]
        [InlineData(false, 5)]
        public async Task DurableEntity_MultipleLockedTransfers(bool extendedSessions, int numberEntities)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LockedTransfer),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_MultipleLockedTransfers),
                extendedSessions))
            {
                await host.StartAsync();

                // create specified number of entities
                var counters = new EntityId[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    counters[i] = new EntityId("Counter", Guid.NewGuid().ToString());
                }

                // in parallel, start one transfer per counter, each decrementing a counter and incrementing
                // its successor (where the last one wraps around to the first)
                // This is a pattern that would deadlock if we didn't order the lock acquisition.
                var clients = new Task<TestDurableClient>[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    clients[i] = host.StartOrchestratorAsync(
                        orchestratorFunctionNames[0],
                        (counters[i], counters[(i + 1) % numberEntities]),
                        this.output);
                }

                var clientResults = await Task.WhenAll(clients);

                // in parallel, wait for all transfers to complete
                var stati = new Task<DurableOrchestrationStatus>[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    stati[i] = clientResults[i].WaitForCompletionAsync(this.output);
                }

                var statiResults = await Task.WhenAll(stati);

                // check that they all completed
                for (int i = 0; i < numberEntities; i++)
                {
                    Assert.Equal(OrchestrationRuntimeStatus.Completed, statiResults[i]?.RuntimeStatus);
                }

                // in parallel, read all the entity states
                var entityStates = new Task<EntityStateResponse<int>>[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    entityStates[i] = clientResults[i].InnerClient.ReadEntityStateAsync<int>(counters[i]);
                }

                var entityStateResults = await Task.WhenAll(entityStates);

                // check that the counter states are all back to 0
                // (since each participated in 2 transfers, one incrementing and one decrementing)
                for (int i = 0; i < numberEntities; i++)
                {
                    Assert.True(entityStateResults[i].EntityExists);
                    Assert.Equal(0, entityStateResults[i].EntityState);
                }

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test which validates that actors can safely make async I/O calls.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableEntity_AsyncIO()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_AsyncIO),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                var entityId = new EntityId("HttpEntity", Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);

                await client.SignalEntity(this.output, "get", "https://www.microsoft.com");
                await client.SignalEntity(this.output, "get", "https://bing.com");

                var state = await client.WaitForEntityState<IDictionary<string, string>>(
                    this.output,
                    TimeSpan.FromSeconds(10),
                    s => s.Count < 2 ? "waiting for both signals to be processed" : null);
                Assert.NotNull(state);

                if (state.TryGetValue("error", out string error))
                {
                    throw new XunitException("Entity encountered an error: " + error);
                }

                Assert.True(state.TryGetValue("https://www.microsoft.com", out string microsoftStatus));
                Assert.Equal("200", microsoftStatus);

                Assert.True(state.TryGetValue("https://bing.com", out string bingStatus));
                Assert.Equal("200", bingStatus);

                Assert.Equal(2, state.Count);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test for EntityId case insensitivity.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityNameCaseInsensitivity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LargeEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityNameCaseInsensitivity),
                extendedSessions))
            {
                await host.StartAsync();

                var entityKey = Guid.NewGuid().ToString();
                var entityName = "StringStore2";

                var entityId = new EntityId(entityName.ToUpperInvariant(), entityKey);

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityId, this.output);

                await client.WaitForCompletionAsync(this.output);

                IDurableEntityClient durableOrchestrationClient = client.InnerClient;

                var response = await durableOrchestrationClient.ReadEntityStateAsync<JToken>(new EntityId(entityName.ToLowerInvariant(), entityKey));

                Assert.True(response.EntityExists);

                await host.StopAsync();
            }
        }

        private static async Task EnsureBlobContainerExists(string containerName)
        {
            var storageConnectionString = TestHelpers.GetStorageConnectionString();
            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
        }
    }
}
