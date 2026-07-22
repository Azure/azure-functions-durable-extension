// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using CoreOrchestrationContext = DurableTask.Core.OrchestrationContext;

namespace Microsoft.Azure.Functions.Worker.Tests;

/// <summary>
/// Regression tests for https://github.com/Azure/azure-functions-durable-extension/issues/2851
/// (and the related https://github.com/Azure/azure-functions-durable-extension/issues/2995):
/// when an orchestration input arrives as a <see cref="JsonElement"/> (the common case, since the
/// inner context deserializes the raw input to <c>object</c>), it must be converted to the target
/// type using the *configured* worker serializer (<see cref="DurableTaskWorkerOptions.DataConverter"/>),
/// not the global Functions <see cref="JsonSerializerOptions"/>. Previously the JsonElement path used
/// the global options, so a user-configured serializer (e.g. camelCase, or IncludeFields for value
/// tuples) was silently ignored and inputs deserialized to null/default.
/// </summary>
public class FunctionsOrchestrationContextInputConverterTests
{
    [Fact]
    public void GetInput_JsonElementValueTuple_UsesConfiguredSerializer_IncludeFields()
    {
        // #2995: value tuples only (de)serialize when IncludeFields = true. Configure it on the worker
        // serializer; the global options intentionally leave it false to prove which one is used.
        var configured = new TestJsonDataConverter(new JsonSerializerOptions { IncludeFields = true });
        JsonElement input = JsonSerializer.SerializeToElement(
            (First: "hello", Last: "world"),
            new JsonSerializerOptions { IncludeFields = true });

        (string First, string Last) result = GetInput<(string First, string Last)>(input, configured);

        Assert.Equal("hello", result.First);
        Assert.Equal("world", result.Last);
    }

    [Fact]
    public void GetInput_JsonElementCamelCase_UsesConfiguredSerializer_NamingPolicy()
    {
        // #2851: a camelCase-configured worker serializer must be honored. The wire payload is camelCase;
        // the default (global) options are case-sensitive and would fail to bind the PascalCase properties.
        var configured = new TestJsonDataConverter(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        JsonElement input = JsonSerializer.SerializeToElement(
            new Person { FirstName = "Ada", LastName = "Lovelace" },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Person? result = GetInput<Person>(input, configured);

        Assert.NotNull(result);
        Assert.Equal("Ada", result!.FirstName);
        Assert.Equal("Lovelace", result.LastName);
    }

    private static T GetInput<T>(JsonElement input, DataConverter configuredConverter)
    {
        var workerOptions = new DurableTaskWorkerOptions { DataConverter = configuredConverter };

        // Global Functions JSON options are intentionally default: they do NOT enable IncludeFields
        // or a camelCase naming policy, so if the JsonElement path (incorrectly) used them the
        // assertions above would fail.
        FunctionContext functionContext = CreateFunctionContext(workerOptions, new JsonSerializerOptions());

        var inner = new Mock<TaskOrchestrationContext>();
        inner.Setup(c => c.GetInput<object>()).Returns(input);
        inner.Setup(c => c.Name).Returns(new TaskName("TestOrchestration"));
        inner.Setup(c => c.InstanceId).Returns("test-instance-id");
        inner.Setup(c => c.Properties).Returns(new Dictionary<string, object?>());

        var context = new FunctionsOrchestrationContext(inner.Object, functionContext);

        // GetInput asserts it runs on the orchestrator thread; emulate that for the duration of the call.
        CoreOrchestrationContext.IsOrchestratorThread = true;
        try
        {
            return context.GetInput<T>();
        }
        finally
        {
            CoreOrchestrationContext.IsOrchestratorThread = false;
        }
    }

    private static FunctionContext CreateFunctionContext(
        DurableTaskWorkerOptions workerOptions, JsonSerializerOptions jsonOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(Options.Create(workerOptions));
        services.AddSingleton(Options.Create(jsonOptions));
        IServiceProvider provider = services.BuildServiceProvider();

        var mock = new Mock<FunctionContext>();
        mock.Setup(c => c.InstanceServices).Returns(provider);
        return mock.Object;
    }

    private sealed class Person
    {
        public string? FirstName { get; set; }

        public string? LastName { get; set; }
    }

    // Mirrors the production ObjectConverterShim: a DataConverter backed by System.Text.Json options.
    private sealed class TestJsonDataConverter : DataConverter
    {
        private readonly JsonSerializerOptions options;

        public TestJsonDataConverter(JsonSerializerOptions options)
        {
            this.options = options;
        }

        public override string? Serialize(object? value)
        {
            return value is null ? null : JsonSerializer.Serialize(value, this.options);
        }

        public override object? Deserialize(string? data, Type targetType)
        {
            return data is null ? null : JsonSerializer.Deserialize(data, targetType, this.options);
        }
    }
}
