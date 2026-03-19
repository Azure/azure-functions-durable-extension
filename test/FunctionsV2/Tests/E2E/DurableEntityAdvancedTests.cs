// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class DurableEntityAdvancedTests : DurableTaskEndToEndTestBase
    {
        public DurableEntityAdvancedTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_BasicObjects(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.BasicObjects),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_BasicObjects),
                extendedSessions))
            {
                await host.StartAsync();

                var chatroom = new EntityId(nameof(TestEntityClasses.ChatRoom), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], chatroom, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("a,b,c", status.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy),
                extendedSessions))
            {
                await host.StartAsync();

                var counter = new EntityId(nameof(TestEntityClasses.CounterWithProxy), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counter, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(true, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy_MultipleInterfaces(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy_MultipleInterfaces),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy_MultipleInterfaces),
                extendedSessions))
            {
                await host.StartAsync();

                var counter = new EntityId(nameof(TestEntityClasses.JobWithProxyMultiInterface), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counter, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(true, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy_UsesBindings(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy),
            };

            string storageConnectionString = TestHelpers.GetStorageConnectionString();
            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(TestEntityClasses.BlobContainerPath);
            await containerClient.CreateIfNotExistsAsync();

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy_UsesBindings),
                extendedSessions))
            {
                await host.StartAsync();

                var counter = new EntityId(nameof(TestEntityClasses.StorageBackedCounter), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counter, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(true, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy_NameResolve(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy_NameResolve),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy_NameResolve),
                extendedSessions))
            {
                await host.StartAsync();

                var entityKey = Guid.NewGuid().ToString();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityKey, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(true, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test which validates that entity state deserialization.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableEntity_CustomIMessageSerializerSettingsFactory(bool classBased)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityWithPrivateSetter),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_CustomIMessageSerializerSettingsFactory),
                enableExtendedSessions: false,
                serializerSettings: new TestEntityClasses.CustomMessageSerializerSettingsFactory()))
            {
                await host.StartAsync();

                string entityName = classBased ? nameof(TestEntities.EntityWithPrivateSetter_C) : nameof(TestEntities.EntityWithPrivateSetter_F);
                var entityKey = new EntityId(entityName, Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityKey, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output.ToString());

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntitiesAsync_FetchState(bool fetchState, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery
            {
                EntityName = "StringStore",
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                FetchState = fetchState,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore", "bar"),
                new EntityId("StringStore", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntitiesAsync_FetchState), storageProvider, query, entityIds);

            Assert.Equal(3, result.Count);

            if (fetchState)
            {
                Assert.NotNull(result[0].State);
            }
            else
            {
                Assert.Null(result[0].State);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntitiesAsync_Paging(bool moreThanOne, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery
            {
                EntityName = "StringStore",
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                PageSize = moreThanOne ? 2 : 1,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore", "bar"),
                new EntityId("StringStore", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntitiesAsync_Paging), storageProvider, query, entityIds);

            Assert.Equal(3, result.Count);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntitiesAsync_NoResults(bool fetchState, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery
            {
                EntityName = "noResult",
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                FetchState = fetchState,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore2", "bar"),
                new EntityId("StringStore2", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntitiesAsync_NoResults), storageProvider, query, entityIds);

            Assert.Empty(result);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntities_Deleted(bool includeDeleted, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery()
            {
                IncludeDeleted = includeDeleted,
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore2", "bar"),
                new EntityId("StringStore2", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            List<string> orchestrations = new List<string>()
            {
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntities_Deleted), storageProvider, query, entityIds, orchestrations);

            Assert.Equal(includeDeleted ? 4 : 2, result.Count);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntities_DeletedPaged(bool includeDeleted, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery()
            {
                IncludeDeleted = includeDeleted,
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                PageSize = 2,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore2", "bar"),
                new EntityId("StringStore2", "baz"),
                new EntityId("StringStore2", "foo"),
                new EntityId("StringStore2", "ffo"),
                new EntityId("StringStore2", "zzz"),
                new EntityId("StringStore2", "aaa"),
                new EntityId("StringStore2", "bbb"),
            };

            List<string> orchestrations = new List<string>()
            {
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntities_DeletedPaged), storageProvider, query, entityIds, orchestrations);

            Assert.Equal(includeDeleted ? 7 : 4, result.Count);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_CleanEntityStorage(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_CleanEntityStorage),
                enableExtendedSessions: false, // we use a failing replay to create the orphaned lock
                entityMessageReorderWindowInMinutes: 0, // need to set this to zero so deleted entities can be removed immediately
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // construct unique names for this test
                string prefix = Guid.NewGuid().ToString("N").Substring(0, 6);
                var emptyEntityId = new EntityId("Counter", $"{prefix}-empty");
                var orphanedEntityId = new EntityId(nameof(TestEntityClasses.CounterWithProxy), $"{prefix}-orphaned");
                var orchestrationA = $"{prefix}-A";
                var orchestrationB = $"{prefix}-B";

                // PART 1: Test removal of empty entities

                // create an empty entity
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CreateEmptyEntities), new EntityId[] { emptyEntityId }, this.output);
                await client.WaitForCompletionAsync(this.output);

                if (storageProvider == TestHelpers.AzureStorageProviderType)
                {
                    // account for delay in updating instance tables
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                // check that the empty entity record is still there in storage
                var query = new EntityQuery
                {
                    EntityName = emptyEntityId.EntityName,
                    IncludeDeleted = true,
                };
                Assert.NotNull(client);
                var result = await client.InnerClient.ListEntitiesAsync(query, CancellationToken.None);
                Assert.Contains(result.Entities, s => s.EntityId.Equals(emptyEntityId));

                // test removal of empty entity
                var response = await client.InnerClient.CleanEntityStorageAsync(removeEmptyEntities: true, releaseOrphanedLocks: false, CancellationToken.None);
                Assert.Equal(1, response.NumberOfEmptyEntitiesRemoved);
                Assert.Equal(0, response.NumberOfOrphanedLocksRemoved);

                // check that the empty entity record has been removed from storage
                result = await client.InnerClient.ListEntitiesAsync(query, CancellationToken.None);
                Assert.DoesNotContain(result.Entities, s => s.EntityId.Equals(emptyEntityId));

                // PART 2: Test recovery from orphaned locks

                // run an orchestration A that leaves an orphaned lock
                TestDurableClient clientA = await host.StartOrchestratorAsync(nameof(TestOrchestrations.LockThenFailReplay), (orphanedEntityId, true), this.output, orchestrationA);
                await clientA.WaitForCompletionAsync(this.output);

                // run an orchestration B that queues behind A for the lock (and thus gets stuck)
                TestDurableClient clientB = await host.StartOrchestratorAsync(nameof(TestOrchestrations.LockThenFailReplay), (orphanedEntityId, false), this.output, orchestrationB);

                // Ensure B has started running (and attempted to acquire the lock) before releasing it
                await clientB.WaitForStartupAsync(this.output);

                // poll until the entity has a stable executionID, rather than using a fixed delay
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    var entityState = await client.InnerClient.ListEntitiesAsync(
                        new EntityQuery { EntityName = orphanedEntityId.EntityName, IncludeDeleted = true },
                        CancellationToken.None);
                    if (entityState.Entities.Any(e => e.EntityId.Equals(orphanedEntityId)))
                    {
                        break;
                    }
                }

                // remove release orphaned lock to unblock orchestration B
                // Note: do NOT remove empty entities yet: we want to keep the empty entity so it can unblock orchestration B
                response = await client.InnerClient.CleanEntityStorageAsync(removeEmptyEntities: false, releaseOrphanedLocks: true, CancellationToken.None);
                Assert.Equal(1, response.NumberOfOrphanedLocksRemoved);
                Assert.Equal(0, response.NumberOfEmptyEntitiesRemoved);

                // wait for orchestration B to complete, now that the lock has been released
                var status = await clientB.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(300));
                Assert.True(status.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

                // Wait for the entity to process B's release message so its LockedBy is cleared.
                // Without this, CleanEntityStorage may see B's lock as orphaned since B is no longer Running.
                var entitySchedulerId = $"@{orphanedEntityId.EntityName}@{orphanedEntityId.EntityKey}";
                for (int i = 0; i < 30; i++)
                {
                    var entityOrchStatus = await client.InnerClient.GetStatusAsync(entitySchedulerId);
                    if (entityOrchStatus == null)
                    {
                        continue;
                    }

                    if (entityOrchStatus.CustomStatus != null)
                    {
                        var entityState = entityOrchStatus.CustomStatus.ToObject<EntityStatus>();
                        if (entityState.LockedBy == null)
                        {
                            break;
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }

                // clean again to remove the orphaned entity which is now empty also
                response = await client.InnerClient.CleanEntityStorageAsync(removeEmptyEntities: true, releaseOrphanedLocks: true, CancellationToken.None);
                Assert.Equal(0, response.NumberOfOrphanedLocksRemoved);
                Assert.Equal(1, response.NumberOfEmptyEntitiesRemoved);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_CleanEntityStorage_Many(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_CleanEntityStorage_Many),
                enableExtendedSessions: false, // we use a failing replay to create the orphaned lock
                entityMessageReorderWindowInMinutes: 0, // need to set this to zero so deleted entities can be removed immediately
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int numReps = 105; // is above the default page size for queries

                // construct unique names for this test
                string prefix = Guid.NewGuid().ToString("N").Substring(0, 6);
                EntityId[] entityIds = new EntityId[numReps];
                for (int i = 0; i < entityIds.Length; i++)
                {
                    entityIds[i] = new EntityId("Counter", $"{prefix}-{i:D3}");
                }

                // create the empty entities
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CreateEmptyEntities), entityIds, this.output);
                await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(120));

                if (storageProvider == TestHelpers.AzureStorageProviderType)
                {
                    // account for delay in updating instance tables
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }

                // remove all empty entities
                var response = await client.InnerClient.CleanEntityStorageAsync(true, true, CancellationToken.None);
                Assert.Equal(0, response.NumberOfOrphanedLocksRemoved);
                Assert.Equal(numReps, response.NumberOfEmptyEntitiesRemoved);

                await host.StopAsync();
            }
        }

        private async Task<IList<DurableEntityStatus>> DurableEntity_ListEntitiesAsync(string taskHub, string storageProvider, EntityQuery query, IList<EntityId> entitiyIds, IList<string> orchestrations = null)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                taskHub,
                true,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                Assert.NotEmpty(entitiyIds);
                TestDurableClient client = null;

                for (int i = 0; i < entitiyIds.Count; i++)
                {
                    EntityId id = entitiyIds[i];
                    string orchestrationName = orchestrations == null ? nameof(TestOrchestrations.EntityId_SignalAndCallStringStore) : orchestrations[i];
                    client = await host.StartOrchestratorAsync(orchestrationName, id, this.output);

                    await client.WaitForCompletionAsync(this.output);
                }

                Assert.NotNull(client);
                TestDurableClient nonNullClient = client;

                if (storageProvider == TestHelpers.AzureStorageProviderType)
                {
                    // account for delay in updating instance tables
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                List<DurableEntityStatus> results = new List<DurableEntityStatus>();

                do
                {
                    var result = await nonNullClient.InnerClient.ListEntitiesAsync(query, CancellationToken.None);

                    // The result may return fewer records than the page size, but never more
                    Assert.True(result.Entities.Count() <= query.PageSize);

                    foreach (var element in result.Entities)
                    {
                        results.Add(element);
                    }

                    query.ContinuationToken = result.ContinuationToken;
                }
                while (query.ContinuationToken != null);

                await host.StopAsync();

                return results;
            }
        }
    }
}
