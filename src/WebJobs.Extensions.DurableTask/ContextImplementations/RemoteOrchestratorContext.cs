// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using DurableTask.Core;
using DurableTask.Core.Command;
using DurableTask.Core.History;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RemoteOrchestratorContext
    {
        private readonly OrchestrationRuntimeState runtimeState;

        private OrchestratorExecutionResult? executionResult;

        public RemoteOrchestratorContext(OrchestrationRuntimeState runtimeState)
        {
            this.runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
        }

        [JsonProperty("instanceId")]
        public string InstanceId => this.runtimeState.OrchestrationInstance.InstanceId;

        [JsonProperty("pastEvents")]
        public IReadOnlyList<HistoryEvent> PastEvents => this.runtimeState.PastEvents;

        [JsonProperty("newEvents")]
        public IEnumerable<HistoryEvent> NewEvents => this.runtimeState.NewEvents;

        [JsonProperty("upperSchemaVersion")]
        internal int UpperSchemaVersion { get; } = 4;

        [JsonIgnore]
        internal bool OrchestratorCompleted { get; private set; }

        [JsonIgnore]
        internal bool ContinuedAsNew { get; private set; }

        [JsonIgnore]
        internal string? SerializedOutput { get; private set; }

        internal void SetResult(string orchestratorResponseJsonText)
        {
            // Validate the JSON since we don't know for sure if the out-of-proc SDK is correctly implemented.
            // We validate the JObject directly instead of the deserialized object since .NET objects don't distinguish
            // between null and not present.
            JObject orchestratorResponseJson = JObject.Parse(orchestratorResponseJsonText);
            if (!orchestratorResponseJson.TryGetValue(nameof(OrchestratorExecutionResult.Actions), StringComparison.OrdinalIgnoreCase, out _) ||
                !orchestratorResponseJson.TryGetValue(nameof(OrchestratorExecutionResult.CustomStatus), StringComparison.OrdinalIgnoreCase, out _))
            {
                throw new ArgumentException(
                    $"Unrecognized orchestrator response payload. First 50 characters: '{orchestratorResponseJson.ToString().Substring(0, 50)}'. " +
                    "Ensure that a compatible SDK is being used to generate the orchestrator response.");
            }

            this.executionResult = orchestratorResponseJson.ToObject<OrchestratorExecutionResult>();

            // Look for an orchestration completion action to see if we need to grab the output.
            foreach (OrchestratorAction action in this.executionResult.Actions)
            {
                if (action is OrchestrationCompleteOrchestratorAction completeAction)
                {
                    this.OrchestratorCompleted = true;
                    this.SerializedOutput = completeAction.Result;
                    this.ContinuedAsNew = completeAction.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew;
                    break;
                }
            }
        }

        internal OrchestratorExecutionResult GetResult()
        {
            return this.executionResult ?? throw new InvalidOperationException($"The execution result has not yet been set using {nameof(this.SetResult)}.");
        }
    }
}
