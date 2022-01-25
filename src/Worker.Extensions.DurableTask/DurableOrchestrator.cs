// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask;
using DurableTask.Core;
using DurableTask.Core.History;
using Newtonsoft.Json;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Static helper class used to execute orchestrator function triggers.
/// </summary>
public static class DurableOrchestrator
{
    // Using Newtonsoft instead of System.Text.Json because Newtonsoft supports a much broader
    // set of features required by DurableTask.Core data types.
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
    };

    /// <summary>
    /// Deserializes orchestration history from <paramref name="triggerStateJson"/> and uses it to execute the orchestrator function
    /// code pointed to by <paramref name="orchestratorFunc"/>.
    /// </summary>
    /// <typeparam name="TInput">The type of the orchestrator function input. This type must be deserializeable from JSON.</typeparam>
    /// <typeparam name="TOutput">The type of the orchestrator function output. This type must be serializeable to JSON.</typeparam>
    /// <param name="triggerStateJson">The trigger state of the orchestrator function. This is expected to be a JSON string but should be treated as internal and opaque.</param>
    /// <param name="orchestratorFunc">A function that implements the orchestrator logic.</param>
    /// <returns>Returns a serialized set of orchestrator actions that should be used as the return value of the orchestrator function trigger.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="triggerStateJson"/> or <paramref name="orchestratorFunc"/> is <c>null</c>.</exception>
    public static string LoadAndRun<TInput, TOutput>(string triggerStateJson, Func<TaskOrchestrationContext, TInput?, Task<TOutput?>> orchestratorFunc)
    {
        if (orchestratorFunc == null)
        {
            throw new ArgumentNullException(nameof(orchestratorFunc));
        }

        FuncTaskOrchestrator<TInput, TOutput> orchestrator = new(orchestratorFunc);
        return LoadAndRun(triggerStateJson, orchestrator);
    }

    /// <summary>
    /// Deserializes orchestration history from <paramref name="triggerStateJson"/> and uses it to resume the orchestrator
    /// implemented by <paramref name="implementation"/>.
    /// </summary>
    /// <param name="triggerStateJson">The trigger state of the orchestrator function. This is expected to be a JSON string but should be treated as internal and opaque.</param>
    /// <param name="implementation">An <see cref="ITaskOrchestrator"/> implementation that defines the orchestrator logic.</param>
    /// <returns>Returns a serialized set of orchestrator actions that should be used as the return value of the orchestrator function trigger.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="triggerStateJson"/> or <paramref name="implementation"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="triggerStateJson"/> contains invalid data.</exception>
    public static string LoadAndRun(string triggerStateJson, ITaskOrchestrator implementation)
    {
        if (string.IsNullOrEmpty(triggerStateJson))
        {
            throw new ArgumentNullException(nameof(triggerStateJson));
        }

        if (implementation == null)
        {
            throw new ArgumentNullException(nameof(implementation));
        }

        OrchestratorState? state = JsonConvert.DeserializeObject<OrchestratorState>(triggerStateJson, SerializerSettings);
        if (state?.PastEvents == null || state?.NewEvents == null)
        {
            throw new ArgumentException(
                $"The {nameof(triggerStateJson)} payload contained invalid data. If this data came from the orchestration trigger binding, it indicates a mismatch between the binding extension used by this app and the WebJobs binding extension being used by the Functions host.",
                nameof(triggerStateJson));
        }

        FunctionsWorkerContext workerContext = new(JsonDataConverter.Default);

        // Re-construct the orchestration state from the history.
        OrchestrationRuntimeState runtimeState = new(state.PastEvents);
        foreach (HistoryEvent newEvent in state.NewEvents)
        {
            runtimeState.AddEvent(newEvent);
        }

        TaskName orchestratorName = new TaskName(runtimeState.Name, runtimeState.Version);

        TaskOrchestrationShim orchestrator = new(workerContext, orchestratorName, implementation);
        TaskOrchestrationExecutor executor = new(runtimeState, orchestrator, BehaviorOnContinueAsNew.Carryover);
        OrchestratorExecutionResult result = executor.Execute();

        return JsonConvert.SerializeObject(result, SerializerSettings);
    }

    private sealed class OrchestratorState
    {
        public string? InstanceId { get; set; }

        public IList<HistoryEvent>? PastEvents { get; set; }

        public IList<HistoryEvent>? NewEvents { get; set; }

        internal int? UpperSchemaVersion { get; set; }
    }

    private sealed class FunctionsWorkerContext : IWorkerContext
    {
        public FunctionsWorkerContext(IDataConverter dataConverter)
        {
            this.DataConverter = dataConverter;
        }

        public IDataConverter DataConverter { get; }
    }
}
