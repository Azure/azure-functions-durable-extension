// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

/// <summary>
/// Function metadata for Durable Functions.
/// </summary>
public sealed class DurableFunctionMetadata : IFunctionMetadata
{
    private static readonly string ScriptFileName = typeof(DurableFunctionMetadata).Assembly.Location;

    private DurableFunctionMetadata(string name, string triggerName, string entryPoint)
    {
        this.Name = name;
        this.EntryPoint = entryPoint;
        this.RawBindings = [
            $$"""
            {
                "type": "{{triggerName}}",
                "name": "context",
                "direction": "In",
                "properties": {}
            }
            """];
        this.FunctionId = this.HashFunctionId();
    }

    /// <inheritdoc/>
    public string? FunctionId { get; }

    /// <inheritdoc/>
    public bool IsProxy => false;

    /// <inheritdoc/>
    public string? Language => "dotnet-isolated";

    /// <inheritdoc/>
    public bool ManagedDependencyEnabled => false;

    /// <inheritdoc/>
    public string? Name { get; }

    /// <inheritdoc/>
    public string? EntryPoint { get; }

    /// <inheritdoc/>
    public IList<string>? RawBindings { get; } = [];

    /// <inheritdoc/>
    public string? ScriptFile => ScriptFileName;

    /// <inheritdoc/>
    public IRetryOptions? Retry => null;

    /// <summary>
    /// Creates metadata for a durable orchestrator function.
    /// </summary>
    /// <param name="name">The name of the orchestrator function.</param>
    /// <returns>A <see cref="DurableFunctionMetadata"/> instance representing the orchestrator.</returns>
    public static DurableFunctionMetadata CreateOrchestrator(string name)
    {
        return new(name, TriggerNames.Orchestration, DurableFunctionExecutor.OrchestrationEntryPoint);
    }

    /// <summary>
    /// Creates metadata for a durable entity function.
    /// </summary>
    /// <param name="name">The name of the entity function.</param>
    /// <returns>A <see cref="DurableFunctionMetadata"/> instance representing the entity.</returns>
    public static DurableFunctionMetadata CreateEntity(string name)
    {
        return new(name, TriggerNames.Entity, DurableFunctionExecutor.EntityEntryPoint);
    }

    /// <summary>
    /// Creates metadata for a durable activity function.
    /// </summary>
    /// <param name="name">The name of the activity function.</param>
    /// <returns>A <see cref="DurableFunctionMetadata"/> instance representing the activity.</returns>
    public static DurableFunctionMetadata CreateActivity(string name)
    {
        return new(name, TriggerNames.Activity, DurableFunctionExecutor.ActivityEntryPoint);
    }

    private string? HashFunctionId()
    {
        // We use uint to avoid the '-' sign when we .ToString() the result.
        // This function is adapted from https://github.com/Azure/azure-functions-host/blob/71ecbb2c303214f96d7e17310681fd717180bdbb/src/WebJobs.Script/Utility.cs#L847-L863
        static uint GetStableHash(string value)
        {
            unchecked
            {
                uint hash = 23;
                foreach (char c in value)
                {
                    hash = (hash * 31) + c;
                }

                return hash;
            }
        }

        unchecked
        {
            uint hash = 17;
            hash = hash * 31 + GetStableHash(this.Name!);
            hash = hash * 31 + GetStableHash(this.ScriptFile!);
            hash = hash * 31 + GetStableHash(this.EntryPoint!);
            return hash.ToString();
        }
    }
}
