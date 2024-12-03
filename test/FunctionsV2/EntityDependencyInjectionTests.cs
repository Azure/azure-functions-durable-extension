// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class EntityDependencyInjectionTests
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;

        public EntityDependencyInjectionTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

#pragma warning disable xUnit1013 // Public method should be marked as test
        public void Dispose()
#pragma warning restore xUnit1013 // Public method should be marked as test
        {
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature with dependency injection.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityWithDependencyInjection(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestEntityWithDependencyInjectionHelpers.EnvironmentOrchestration),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityWithDependencyInjection),
                extendedSessions,
                nameResolver: new TestEntityWithDependencyInjectionHelpers.DummyEnvironmentVariableResolver()))
            {
                await host.StartAsync();

                var environment = new EntityId(nameof(TestEntityWithDependencyInjectionHelpers.Environment), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], environment, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(TestEntityWithDependencyInjectionHelpers.DummyEnvironmentVariableValue, status?.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature with dependency injection.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_DependencyInjectionAndBindings(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestEntityWithDependencyInjectionHelpers.BlobEnvironmentOrchestration),
            };

            string storageConnectionString = TestHelpers.GetStorageConnectionString();
            var blobServiceClient = new BlobServiceClient(storageConnectionString);

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(TestEntityWithDependencyInjectionHelpers.BlobContainerPath);
            await containerClient.CreateIfNotExistsAsync();

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_DependencyInjectionAndBindings),
                extendedSessions,
                nameResolver: new TestEntityWithDependencyInjectionHelpers.DummyEnvironmentVariableResolver()))
            {
                await host.StartAsync();

                var environment = new EntityId(nameof(TestEntityWithDependencyInjectionHelpers.BlobBackedEnvironment), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], environment, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                List<string> outputValues = status?.Output?.ToObject<List<string>>();
                Assert.NotNull(outputValues);
                Assert.Equal(TestEntityWithDependencyInjectionHelpers.DummyEnvironmentVariableValue, outputValues[0]);
                Assert.Equal(TestEntityWithDependencyInjectionHelpers.BlobStoredEnvironmentVariableValue, outputValues[1]);
                await host.StopAsync();
            }
        }
    }
}
