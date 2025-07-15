// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.Durable.Tests.E2E;

public class ClassBasedEntities
{
    [Function(nameof(ClassBasedEntityOrchestration))]
    public static async Task<string> ClassBasedEntityOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = new EntityInstanceId(nameof(TestEntity), "singleton");
        await context.Entities.CallEntityAsync(entityId, nameof(TestEntity.SetState), 42);
        return await context.Entities.CallEntityAsync<string>(entityId, nameof(TestEntity.GetState));
    }

    [Function(nameof(TestEntity))]
    public static Task TestEntityDispatcher(
        [EntityTrigger] TaskEntityDispatcher dispatcher,
        [BlobInput("azure-webjobs-hosts")] BlobContainerClient container)
    {
        return dispatcher.DispatchAsync<TestEntity>(container);
    }

    private class TestEntity(
        IConfiguration injectedConfiguration,
        MyInjectedService testService,
        BlobContainerClient container) : TaskEntity<StateContainer>
    {
        private readonly IConfiguration injectedConfiguration = injectedConfiguration;
        private readonly MyInjectedService testService = testService;
        private readonly BlobContainerClient container = container;

        public void SetState(int number)
        {
            this.State ??= new StateContainer();
            this.State.StringValue = string.Format(
                "IConfiguration: {0}, MyInjectedService: {1}, BlobContainerClient: {2}, Number: {3}",
                this.injectedConfiguration is not null ? "yes" : "no",
                this.testService is not null ? "yes" : "no",
                this.container is not null ? "yes" : "no",
                number);
        }

        public string GetState()
        {
            // Expected value: "IConfiguration: yes, MyInjectedService: yes, BlobContainerClient: yes"
            return this.State.StringValue;
        }
    }

    private class StateContainer
    {
        public string StringValue { get; set; } = string.Empty;
    }
}
