// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class DurableFunctionMetadataTests
{
    [Fact]
    public void CreateOrchestrator_ShouldSetCorrectProperties()
    {
        // Arrange
        string functionName = "MyOrchestrator";

        // Act
        DurableFunctionMetadata metadata = DurableFunctionMetadata.CreateOrchestrator(functionName);

        // Assert
        Assert.Equal(functionName, metadata.Name);
        Assert.Equal(DurableFunctionExecutor.OrchestrationEntryPoint, metadata.EntryPoint);
        Assert.Equal("dotnet-isolated", metadata.Language);
        Assert.False(metadata.IsProxy);
        Assert.False(metadata.ManagedDependencyEnabled);
        Assert.Null(metadata.Retry);
        Assert.NotNull(metadata.FunctionId);
        Assert.NotNull(metadata.ScriptFile);
        Assert.NotNull(metadata.RawBindings);
        Assert.Single(metadata.RawBindings);
        Assert.Contains("orchestrationTrigger", metadata.RawBindings[0]);
    }

    [Fact]
    public void CreateEntity_ShouldSetCorrectProperties()
    {
        // Arrange
        string functionName = "MyEntity";

        // Act
        DurableFunctionMetadata metadata = DurableFunctionMetadata.CreateEntity(functionName);

        // Assert
        Assert.Equal(functionName, metadata.Name);
        Assert.Equal(DurableFunctionExecutor.EntityEntryPoint, metadata.EntryPoint);
        Assert.Equal("dotnet-isolated", metadata.Language);
        Assert.False(metadata.IsProxy);
        Assert.False(metadata.ManagedDependencyEnabled);
        Assert.Null(metadata.Retry);
        Assert.NotNull(metadata.FunctionId);
        Assert.NotNull(metadata.ScriptFile);
        Assert.NotNull(metadata.RawBindings);
        Assert.Single(metadata.RawBindings);
        Assert.Contains("entityTrigger", metadata.RawBindings[0]);
    }

    [Fact]
    public void CreateActivity_ShouldSetCorrectProperties()
    {
        // Arrange
        string functionName = "MyActivity";

        // Act
        DurableFunctionMetadata metadata = DurableFunctionMetadata.CreateActivity(functionName);

        // Assert
        Assert.Equal(functionName, metadata.Name);
        Assert.Equal(DurableFunctionExecutor.ActivityEntryPoint, metadata.EntryPoint);
        Assert.Equal("dotnet-isolated", metadata.Language);
        Assert.False(metadata.IsProxy);
        Assert.False(metadata.ManagedDependencyEnabled);
        Assert.Null(metadata.Retry);
        Assert.NotNull(metadata.FunctionId);
        Assert.NotNull(metadata.ScriptFile);
        Assert.NotNull(metadata.RawBindings);
        Assert.Single(metadata.RawBindings);
        Assert.Contains("activityTrigger", metadata.RawBindings[0]);
    }

    [Fact]
    public void FunctionId_ShouldBeConsistentForSameName()
    {
        // Arrange
        string functionName = "TestFunction";

        // Act
        DurableFunctionMetadata metadata1 = DurableFunctionMetadata.CreateOrchestrator(functionName);
        DurableFunctionMetadata metadata2 = DurableFunctionMetadata.CreateOrchestrator(functionName);

        // Assert
        Assert.Equal(metadata1.FunctionId, metadata2.FunctionId);
    }

    [Fact]
    public void FunctionId_ShouldBeDifferentForDifferentNames()
    {
        // Arrange & Act
        DurableFunctionMetadata metadata1 = DurableFunctionMetadata.CreateOrchestrator("Function1");
        DurableFunctionMetadata metadata2 = DurableFunctionMetadata.CreateOrchestrator("Function2");

        // Assert
        Assert.NotEqual(metadata1.FunctionId, metadata2.FunctionId);
    }

    [Fact]
    public void FunctionId_ShouldBeDifferentForDifferentTypes()
    {
        // Arrange
        string functionName = "TestFunction";

        // Act
        DurableFunctionMetadata orchestrator = DurableFunctionMetadata.CreateOrchestrator(functionName);
        DurableFunctionMetadata activity = DurableFunctionMetadata.CreateActivity(functionName);
        DurableFunctionMetadata entity = DurableFunctionMetadata.CreateEntity(functionName);

        // Assert - Different entry points should result in different function IDs
        Assert.NotEqual(orchestrator.FunctionId, activity.FunctionId);
        Assert.NotEqual(orchestrator.FunctionId, entity.FunctionId);
        Assert.NotEqual(activity.FunctionId, entity.FunctionId);
    }

    [Fact]
    public void RawBindings_ShouldContainContextParameter()
    {
        // Arrange & Act
        DurableFunctionMetadata metadata = DurableFunctionMetadata.CreateOrchestrator("TestFunction");

        // Assert
        Assert.Contains("\"name\": \"context\"", metadata.RawBindings?[0]);
        Assert.Contains("\"direction\": \"In\"", metadata.RawBindings?[0]);
    }
}
