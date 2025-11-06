// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class DurableTaskRegistryExtensionsTests
{
    [Fact]
    public void GetOrchestrators_WithNullRegistry_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            DurableTaskRegistryExtensions.GetOrchestrators(null!));
    }

    [Fact]
    public void GetOrchestrators_WithEmptyRegistry_ShouldReturnEmpty()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();

        // Act
        IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskOrchestrator>>> orchestrators = registry.GetOrchestrators();

        // Assert
        Assert.Empty(orchestrators);
    }

    [Fact]
    public void GetOrchestrators_WithRegisteredOrchestrators_ShouldReturnThem()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddOrchestrator<TestOrchestrator>("Orchestrator1");
        registry.AddOrchestrator<TestOrchestrator>("Orchestrator2");

        // Act
        List<KeyValuePair<TaskName, Func<IServiceProvider, ITaskOrchestrator>>> orchestrators = registry.GetOrchestrators().ToList();

        // Assert
        Assert.Equal(2, orchestrators.Count);
        Assert.Contains(orchestrators, kvp => kvp.Key.ToString() == "Orchestrator1");
        Assert.Contains(orchestrators, kvp => kvp.Key.ToString() == "Orchestrator2");
    }

    [Fact]
    public void GetActivities_WithNullRegistry_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            DurableTaskRegistryExtensions.GetActivities(null!));
    }

    [Fact]
    public void GetActivities_WithEmptyRegistry_ShouldReturnEmpty()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();

        // Act
        IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskActivity>>> activities = registry.GetActivities();

        // Assert
        Assert.Empty(activities);
    }

    [Fact]
    public void GetActivities_WithRegisteredActivities_ShouldReturnThem()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddActivity<TestActivity>("Activity1");
        registry.AddActivity<TestActivity>("Activity2");

        // Act
        List<KeyValuePair<TaskName, Func<IServiceProvider, ITaskActivity>>> activities = registry.GetActivities().ToList();

        // Assert
        Assert.Equal(2, activities.Count);
        Assert.Contains(activities, kvp => kvp.Key.ToString() == "Activity1");
        Assert.Contains(activities, kvp => kvp.Key.ToString() == "Activity2");
    }

    [Fact]
    public void GetEntities_WithNullRegistry_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            DurableTaskRegistryExtensions.GetEntities(null!));
    }

    [Fact]
    public void GetEntities_WithEmptyRegistry_ShouldReturnEmpty()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();

        // Act
        IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskEntity>>> entities = registry.GetEntities();

        // Assert
        Assert.Empty(entities);
    }

    [Fact]
    public void GetEntities_WithRegisteredEntities_ShouldReturnThem()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddEntity<TestEntity>("Entity1");
        registry.AddEntity<TestEntity>("Entity2");

        // Act
        List<KeyValuePair<TaskName, Func<IServiceProvider, ITaskEntity>>> entities = registry.GetEntities().ToList();

        // Assert
        Assert.Equal(2, entities.Count);
        Assert.Contains(entities, kvp => kvp.Key.ToString() == "Entity1");
        Assert.Contains(entities, kvp => kvp.Key.ToString() == "Entity2");
    }

    [Fact]
    public void GetOrchestrators_ShouldNotReturnActivitiesOrEntities()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddOrchestrator<TestOrchestrator>("Orchestrator1");
        registry.AddActivity<TestActivity>("Activity1");
        registry.AddEntity<TestEntity>("Entity1");

        // Act
        List<KeyValuePair<TaskName, Func<IServiceProvider, ITaskOrchestrator>>> orchestrators = registry.GetOrchestrators().ToList();

        // Assert
        Assert.Single(orchestrators);
        Assert.Equal("Orchestrator1", orchestrators[0].Key.ToString());
    }

    [Fact]
    public void GetActivities_ShouldNotReturnOrchestratorsOrEntities()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddOrchestrator<TestOrchestrator>("Orchestrator1");
        registry.AddActivity<TestActivity>("Activity1");
        registry.AddEntity<TestEntity>("Entity1");

        // Act
        List<KeyValuePair<TaskName, Func<IServiceProvider, ITaskActivity>>> activities = registry.GetActivities().ToList();

        // Assert
        Assert.Single(activities);
        Assert.Equal("Activity1", activities[0].Key.ToString());
    }

    [Fact]
    public void GetEntities_ShouldNotReturnOrchestratorsOrActivities()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddOrchestrator<TestOrchestrator>("Orchestrator1");
        registry.AddActivity<TestActivity>("Activity1");
        registry.AddEntity<TestEntity>("Entity1");

        // Act
        List<KeyValuePair<TaskName, Func<IServiceProvider, ITaskEntity>>> entities = registry.GetEntities().ToList();

        // Assert
        Assert.Single(entities);
        Assert.Equal("Entity1", entities[0].Key.ToString());
    }

    private class TestOrchestrator : TaskOrchestrator<object, object>
    {
        public override Task<object> RunAsync(TaskOrchestrationContext context, object input)
        {
            return Task.FromResult<object>(string.Empty);
        }
    }

    private class TestActivity : TaskActivity<object, object>
    {
        public override Task<object> RunAsync(TaskActivityContext context, object input)
        {
            return Task.FromResult<object>(string.Empty);
        }
    }

    private class TestEntity : TaskEntity<string>
    {
        public Task RunAsync(TaskEntityContext context)
        {
            return Task.CompletedTask;
        }
    }
}
