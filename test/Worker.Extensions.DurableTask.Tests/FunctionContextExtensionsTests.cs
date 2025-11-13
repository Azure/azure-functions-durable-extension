// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class FunctionContextExtensionsTests
{
    [Fact]
    public void TryGetOrchestrationBinding_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            FunctionContextExtensions.TryGetOrchestrationBinding(null!, out _));
    }

    [Fact]
    public void TryGetOrchestrationBinding_WithOrchestrationTrigger_ShouldReturnTrue()
    {
        // Arrange
        FunctionContext context = CreateContextWithBinding(TriggerNames.Orchestration);

        // Act
        bool result = context.TryGetOrchestrationBinding(out BindingMetadata? binding);

        // Assert
        Assert.True(result);
        Assert.NotNull(binding);
        Assert.Equal(TriggerNames.Orchestration, binding.Type);
    }

    [Fact]
    public void TryGetOrchestrationBinding_WithoutOrchestrationTrigger_ShouldReturnFalse()
    {
        // Arrange
        FunctionContext context = CreateContextWithBinding(TriggerNames.Activity);

        // Act
        bool result = context.TryGetOrchestrationBinding(out BindingMetadata? binding);

        // Assert
        Assert.False(result);
        Assert.Null(binding);
    }

    [Fact]
    public void TryGetActivityBinding_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            FunctionContextExtensions.TryGetActivityBinding(null!, out _));
    }

    [Fact]
    public void TryGetActivityBinding_WithActivityTrigger_ShouldReturnTrue()
    {
        // Arrange
        FunctionContext context = CreateContextWithBinding(TriggerNames.Activity);

        // Act
        bool result = context.TryGetActivityBinding(out BindingMetadata? binding);

        // Assert
        Assert.True(result);
        Assert.NotNull(binding);
        Assert.Equal(TriggerNames.Activity, binding.Type);
    }

    [Fact]
    public void TryGetActivityBinding_WithoutActivityTrigger_ShouldReturnFalse()
    {
        // Arrange
        FunctionContext context = CreateContextWithBinding(TriggerNames.Entity);

        // Act
        bool result = context.TryGetActivityBinding(out BindingMetadata? binding);

        // Assert
        Assert.False(result);
        Assert.Null(binding);
    }

    [Fact]
    public void TryGetEntityBinding_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            FunctionContextExtensions.TryGetEntityBinding(null!, out _));
    }

    [Fact]
    public void TryGetEntityBinding_WithEntityTrigger_ShouldReturnTrue()
    {
        // Arrange
        FunctionContext context = CreateContextWithBinding(TriggerNames.Entity);

        // Act
        bool result = context.TryGetEntityBinding(out BindingMetadata? binding);

        // Assert
        Assert.True(result);
        Assert.NotNull(binding);
        Assert.Equal(TriggerNames.Entity, binding.Type);
    }

    [Fact]
    public void TryGetEntityBinding_WithoutEntityTrigger_ShouldReturnFalse()
    {
        // Arrange
        FunctionContext context = CreateContextWithBinding(TriggerNames.Orchestration);

        // Act
        bool result = context.TryGetEntityBinding(out BindingMetadata? binding);

        // Assert
        Assert.False(result);
        Assert.Null(binding);
    }

    [Fact]
    public void TryGetBinding_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            FunctionContextExtensions.TryGetBinding(null!, "anyTrigger", out _));
    }

    [Fact]
    public void TryGetBinding_WithMatchingTrigger_ShouldReturnTrue()
    {
        // Arrange
        string triggerName = "customTrigger";
        FunctionContext context = CreateContextWithBinding(triggerName);

        // Act
        bool result = context.TryGetBinding(triggerName, out BindingMetadata? binding);

        // Assert
        Assert.True(result);
        Assert.NotNull(binding);
        Assert.Equal(triggerName, binding.Type);
    }

    [Fact]
    public void TryGetBinding_IsCaseInsensitive()
    {
        // Arrange
        FunctionContext context = CreateContextWithBinding("orchestrationTrigger");

        // Act
        bool result = context.TryGetBinding("ORCHESTRATIONTRIGGER", out BindingMetadata? binding);

        // Assert
        Assert.True(result);
        Assert.NotNull(binding);
    }

    [Fact]
    public void GetInstanceId_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            FunctionContextExtensions.GetInstanceId(null!));
    }

    [Fact]
    public void GetInstanceId_ShouldReturnInstanceIdFromBindingData()
    {
        // Arrange
        string expectedInstanceId = "test-instance-123";
        FunctionContext context = CreateContextWithInstanceId(expectedInstanceId);

        // Act
        string actualInstanceId = context.GetInstanceId();

        // Assert
        Assert.Equal(expectedInstanceId, actualInstanceId);
    }

    private static FunctionContext CreateContextWithBinding(string triggerType)
    {
        TestBindingMetadata binding = new TestBindingMetadata(triggerType);
        Dictionary<string, BindingMetadata> bindingDict = new Dictionary<string, BindingMetadata>
        {
            ["trigger"] = binding
        };

        return new TestFunctionContext(bindingDict, new Dictionary<string, object?>());
    }

    private static FunctionContext CreateContextWithInstanceId(string instanceId)
    {
        Dictionary<string, object?> bindingDataDict = new Dictionary<string, object?>
        {
            ["instanceId"] = instanceId
        };
        return new TestFunctionContext(new Dictionary<string, BindingMetadata>(), bindingDataDict);
    }

    class TestFunctionContext : FunctionContext
    {
        public TestFunctionContext(IDictionary<string, BindingMetadata> inputBindings, IDictionary<string, object?> bindingContext)
        {
            this.FunctionDefinition = new TestFunctionDefinition(inputBindings);
            this.BindingContext = new TestBindingContext(bindingContext);
        }

        public override string InvocationId => throw new NotImplementedException();

        public override string FunctionId => throw new NotImplementedException();

        public override TraceContext TraceContext => throw new NotImplementedException();

        public override BindingContext BindingContext { get; }

        public override RetryContext RetryContext => throw new NotImplementedException();

        public override IServiceProvider InstanceServices
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override FunctionDefinition FunctionDefinition { get; }

        public override IDictionary<object, object> Items
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override IInvocationFeatures Features => throw new NotImplementedException();
    }

    class TestFunctionDefinition : FunctionDefinition
    {
        public TestFunctionDefinition(IDictionary<string, BindingMetadata> inputBindings)
        {
            this.InputBindings = inputBindings.ToImmutableDictionary();
        }

        public override string Name => throw new NotImplementedException();
        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; }
        public override IImmutableDictionary<string, BindingMetadata> OutputBindings => throw new NotImplementedException();

        public override ImmutableArray<FunctionParameter> Parameters => throw new NotImplementedException();

        public override string PathToAssembly => throw new NotImplementedException();

        public override string EntryPoint => throw new NotImplementedException();

        public override string Id => throw new NotImplementedException();
    }

    class TestBindingMetadata : BindingMetadata
    {
        public TestBindingMetadata(string type)
        {
            this.Type = type;
        }

        public override string Name { get; } = string.Empty;
        public override string Type { get; } = string.Empty;
        public override BindingDirection Direction { get; } = BindingDirection.In;
    }

    class TestBindingContext : BindingContext
    {
        public TestBindingContext(IDictionary<string, object?> bindingData)
        {
            this.BindingData = bindingData.AsReadOnly();
        }
        public override IReadOnlyDictionary<string, object?> BindingData { get; }
    }
}
