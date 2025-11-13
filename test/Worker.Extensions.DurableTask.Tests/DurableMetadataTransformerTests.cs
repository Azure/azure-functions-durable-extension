// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class DurableMetadataTransformerTests
{
    [Fact]
    public void Constructor_WithNullRegistry_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DurableMetadataTransformer(null!));
    }

    [Fact]
    public void Transform_WithNullOriginal_ShouldThrowArgumentNullException()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        IOptions<DurableTaskRegistry> options = Options.Create(registry);
        DurableMetadataTransformer transformer = new DurableMetadataTransformer(options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => transformer.Transform(null!));
    }

    [Fact]
    public void Transform_WithEmptyRegistry_ShouldNotAddAnyMetadata()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        IOptions<DurableTaskRegistry> options = Options.Create(registry);
        DurableMetadataTransformer transformer = new DurableMetadataTransformer(options);
        List<IFunctionMetadata> original = new List<IFunctionMetadata>();

        // Act
        transformer.Transform(original);

        // Assert
        Assert.Empty(original);
    }

    [Fact]
    public void Transform_WithOrchestrators_ShouldAddOrchestratorMetadata()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddOrchestrator<TestOrchestrator>("TestOrchestrator");
        IOptions<DurableTaskRegistry> options = Options.Create(registry);
        DurableMetadataTransformer transformer = new DurableMetadataTransformer(options);
        List<IFunctionMetadata> original = new List<IFunctionMetadata>();

        // Act
        transformer.Transform(original);

        // Assert
        Assert.Single(original);
        DurableFunctionMetadata metadata = Assert.IsType<DurableFunctionMetadata>(original[0]);
        Assert.Equal("TestOrchestrator", metadata.Name);
        Assert.Contains("orchestrationTrigger", metadata.RawBindings![0]);
    }

    [Fact]
    public void Transform_WithActivities_ShouldAddActivityMetadata()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddActivity<TestActivity>("TestActivity");
        IOptions<DurableTaskRegistry> options = Options.Create(registry);
        DurableMetadataTransformer transformer = new DurableMetadataTransformer(options);
        List<IFunctionMetadata> original = new List<IFunctionMetadata>();

        // Act
        transformer.Transform(original);

        // Assert
        Assert.Single(original);
        DurableFunctionMetadata metadata = Assert.IsType<DurableFunctionMetadata>(original[0]);
        Assert.Equal("TestActivity", metadata.Name);
        Assert.Contains("activityTrigger", metadata.RawBindings![0]);
    }

    [Fact]
    public void Transform_WithEntities_ShouldAddEntityMetadata()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddEntity<TestEntity>("TestEntity");
        IOptions<DurableTaskRegistry> options = Options.Create(registry);
        DurableMetadataTransformer transformer = new DurableMetadataTransformer(options);
        List<IFunctionMetadata> original = new List<IFunctionMetadata>();

        // Act
        transformer.Transform(original);

        // Assert
        Assert.Single(original);
        DurableFunctionMetadata metadata = Assert.IsType<DurableFunctionMetadata>(original[0]);
        Assert.Equal("TestEntity", metadata.Name);
        Assert.Contains("entityTrigger", metadata.RawBindings![0]);
    }

    [Fact]
    public void Transform_WithMultipleTypes_ShouldAddAllMetadata()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        registry.AddOrchestrator<TestOrchestrator>("Orchestrator1");
        registry.AddOrchestrator<TestOrchestrator>("Orchestrator2");
        registry.AddActivity<TestActivity>("Activity1");
        registry.AddEntity<TestEntity>("Entity1");
        IOptions<DurableTaskRegistry> options = Options.Create(registry);
        DurableMetadataTransformer transformer = new DurableMetadataTransformer(options);
        List<IFunctionMetadata> original = new List<IFunctionMetadata>();

        // Act
        transformer.Transform(original);

        // Assert
        Assert.Equal(4, original.Count);
        Assert.Contains(original, m => m.Name == "Orchestrator1");
        Assert.Contains(original, m => m.Name == "Orchestrator2");
        Assert.Contains(original, m => m.Name == "Activity1");
        Assert.Contains(original, m => m.Name == "Entity1");
    }

    [Fact]
    public void Name_ShouldReturnExpectedValue()
    {
        // Arrange
        DurableTaskRegistry registry = new DurableTaskRegistry();
        IOptions<DurableTaskRegistry> options = Options.Create(registry);
        DurableMetadataTransformer transformer = new DurableMetadataTransformer(options);

        // Act
        string name = transformer.Name;

        // Assert
        Assert.Equal("DurableMetadataTransformer", name);
    }

    private class TestOrchestrator : TaskOrchestrator<object?, object?>
    {
        public override Task<object?> RunAsync(TaskOrchestrationContext context, object? input)
        {
            return Task.FromResult<object?>(new object());
        }
    }

    private class TestActivity : TaskActivity<object?, object?>
    {
        public override Task<object?> RunAsync(TaskActivityContext context, object? input)
        {
            return Task.FromResult<object?>(new object());
        }
    }

    private class TestEntity : TaskEntity<object>
    {
    }
}
