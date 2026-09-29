// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableClientQueryTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ListInstancesAsync_ExcludeEntities(bool? excludeEntities)
        {
            (IDurableClient client, Mock<DurabilityProvider> provider) = CreateClient();
            var condition = new OrchestrationStatusQueryCondition { ContinuationToken = "previous-page" };
            if (excludeEntities.HasValue)
            {
                condition.ExcludeEntities = excludeEntities.Value;
            }

            var orchestration = new DurableOrchestrationStatus { InstanceId = "orchestration@01" };
            var entity = new DurableOrchestrationStatus { InstanceId = "@counter@01" };
            var providerResult = new OrchestrationStatusQueryResult
            {
                DurableOrchestrationState = new[] { entity, orchestration },
                ContinuationToken = "next-page",
            };
            using var cancellation = new CancellationTokenSource();
            provider.Setup(x => x.GetOrchestrationStateWithPagination(condition, cancellation.Token))
                .ReturnsAsync(providerResult);

            OrchestrationStatusQueryResult result = await client.ListInstancesAsync(condition, cancellation.Token);

            Assert.Equal("next-page", result.ContinuationToken);
            if (excludeEntities == true)
            {
                Assert.Same(orchestration, Assert.Single(result.DurableOrchestrationState));
            }
            else
            {
                Assert.Same(providerResult, result);
                Assert.Equal(new[] { entity, orchestration }, result.DurableOrchestrationState);
            }

            Assert.Equal(2, providerResult.DurableOrchestrationState.Count());
            provider.Verify(x => x.GetOrchestrationStateWithPagination(condition, cancellation.Token), Times.Once());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("@")]
        [InlineData("@counter@")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ListInstancesAsync_ExcludeEntities_WithInstanceIdPrefix(string instanceIdPrefix)
        {
            (IDurableClient client, Mock<DurabilityProvider> provider) = CreateClient();
            var condition = new OrchestrationStatusQueryCondition
            {
                ExcludeEntities = true,
                InstanceIdPrefix = instanceIdPrefix,
            };
            provider.Setup(x => x.GetOrchestrationStateWithPagination(condition, CancellationToken.None))
                .ReturnsAsync(new OrchestrationStatusQueryResult
                {
                    DurableOrchestrationState = new[]
                    {
                        new DurableOrchestrationStatus { InstanceId = "@counter@01" },
                    },
                });

            OrchestrationStatusQueryResult result = await client.ListInstancesAsync(condition, CancellationToken.None);

            Assert.Empty(result.DurableOrchestrationState);
            Assert.Null(result.ContinuationToken);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ListInstancesAsync_ExcludeEntities_PreservesContinuationThroughEmptyPages()
        {
            (IDurableClient client, Mock<DurabilityProvider> provider) = CreateClient();
            var condition = new OrchestrationStatusQueryCondition { ExcludeEntities = true, PageSize = 1 };
            provider.Setup(x => x.GetOrchestrationStateWithPagination(
                    It.Is<OrchestrationStatusQueryCondition>(q => q.ContinuationToken == null), CancellationToken.None))
                .ReturnsAsync(new OrchestrationStatusQueryResult
                {
                    DurableOrchestrationState = new[]
                    {
                        new DurableOrchestrationStatus { InstanceId = "@counter@01" },
                    },
                    ContinuationToken = "next-page",
                });
            provider.Setup(x => x.GetOrchestrationStateWithPagination(
                    It.Is<OrchestrationStatusQueryCondition>(q => q.ContinuationToken == "next-page"), CancellationToken.None))
                .ReturnsAsync(new OrchestrationStatusQueryResult
                {
                    DurableOrchestrationState = new[]
                    {
                        new DurableOrchestrationStatus { InstanceId = "orchestration" },
                    },
                });

            OrchestrationStatusQueryResult firstPage = await client.ListInstancesAsync(condition, CancellationToken.None);

            Assert.Empty(firstPage.DurableOrchestrationState);
            Assert.Equal("next-page", firstPage.ContinuationToken);
            provider.Verify(x => x.GetOrchestrationStateWithPagination(condition, CancellationToken.None), Times.Once());

            condition.ContinuationToken = firstPage.ContinuationToken;
            OrchestrationStatusQueryResult secondPage = await client.ListInstancesAsync(condition, CancellationToken.None);

            Assert.Equal("orchestration", Assert.Single(secondPage.DurableOrchestrationState).InstanceId);
            Assert.Null(secondPage.ContinuationToken);
            provider.Verify(x => x.GetOrchestrationStateWithPagination(condition, CancellationToken.None), Times.Exactly(2));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("counter")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ListEntitiesAsync_LegacyQuery_StillIncludesEntities(string entityName)
        {
            (IDurableClient client, Mock<DurabilityProvider> provider) = CreateClient();
            provider.Setup(x => x.GetOrchestrationStateWithPagination(It.IsAny<OrchestrationStatusQueryCondition>(), CancellationToken.None))
                .ReturnsAsync(new OrchestrationStatusQueryResult
                {
                    DurableOrchestrationState = new[]
                    {
                        new DurableOrchestrationStatus
                        {
                            InstanceId = "@counter@01",
                            CustomStatus = new JObject { ["entityExists"] = true },
                        },
                    },
                });

            EntityQueryResult result = await client.ListEntitiesAsync(
                new EntityQuery { EntityName = entityName }, CancellationToken.None);

            Assert.Equal(new EntityId("counter", "01"), Assert.Single(result.Entities).EntityId);
            provider.Verify(
                x => x.GetOrchestrationStateWithPagination(
                    It.Is<OrchestrationStatusQueryCondition>(q => !q.ExcludeEntities
                        && q.InstanceIdPrefix == (entityName == null ? "@" : "@counter@")),
                    CancellationToken.None),
                Times.Once());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ListInstancesAsync_PropagatesProviderFailure(bool excludeEntities)
        {
            (IDurableClient client, Mock<DurabilityProvider> provider) = CreateClient();
            var condition = new OrchestrationStatusQueryCondition { ExcludeEntities = excludeEntities };
            var failure = new InvalidOperationException("Query failed.");
            provider.Setup(x => x.GetOrchestrationStateWithPagination(condition, CancellationToken.None))
                .ThrowsAsync(failure);

            InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.ListInstancesAsync(condition, CancellationToken.None));

            Assert.Same(failure, actual);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ListInstancesAsync_PropagatesCancellation(bool excludeEntities)
        {
            (IDurableClient client, Mock<DurabilityProvider> provider) = CreateClient();
            var condition = new OrchestrationStatusQueryCondition { ExcludeEntities = excludeEntities };
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            provider.Setup(x => x.GetOrchestrationStateWithPagination(condition, cancellation.Token))
                .Returns(Task.FromCanceled<OrchestrationStatusQueryResult>(cancellation.Token));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.ListInstancesAsync(condition, cancellation.Token));
        }

        private static (IDurableClient Client, Mock<DurabilityProvider> Provider) CreateClient()
        {
            var provider = new Mock<DurabilityProvider>(
                "test",
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "test");
            var options = new DurableTaskOptions { HubName = "QueryTestHub" };
            var converter = new MessagePayloadDataConverter(new JsonSerializerSettings(), true);
            var traceHelper = new EndToEndTraceHelper(
                new NullLogger<EndToEndTraceHelper>(), options.Tracing.TraceReplayEvents);
            var client = new DurableClient(
                provider.Object,
                httpHandler: null,
                new DurableClientAttribute { TaskHub = options.HubName },
                converter,
                traceHelper,
                options);

            return (client, provider);
        }
    }
}
