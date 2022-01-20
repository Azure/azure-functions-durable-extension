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

// TODO: Documentation
public static class DurableOrchestrator
{
    // Using Newtonsoft instead of System.Text.Json because Newtonsoft supports a much broader
    // set of features required by DurableTask.Core data types.
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
    };

    // TODO: Documentation
    public static string LoadAndRun<TInput, TOutput>(string triggerStateJson, Func<TaskOrchestrationContext, TInput?, Task<TOutput?>> orchestratorFunc)
    {
        if (orchestratorFunc == null)
        {
            throw new ArgumentNullException(nameof(orchestratorFunc));
        }

        FuncTaskOrchestrator<TInput, TOutput> orchestrator = new(orchestratorFunc);
        return LoadAndRun(triggerStateJson, orchestrator);
    }

    // TODO: Documentation
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

        OrchestratorState state = JsonConvert.DeserializeObject<OrchestratorState>(triggerStateJson, SerializerSettings);
        if (state.PastEvents == null || state.NewEvents == null)
        {
            throw new InvalidOperationException("Invalid data was received from the orchestration binding. This indicates a mismatch between the binding extension used by this app and the WebJobs binding extension being used by the Functions host.");
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
