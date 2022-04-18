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
        public string InstanceId => this.runtimeState.OrchestrationInstance?.InstanceId ?? string.Empty;

        [JsonProperty("pastEvents")]
        public IEnumerable<HistoryEvent> PastEvents => this.runtimeState.PastEvents;

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

        internal void SetResult(IEnumerable<OrchestratorAction> actions, string customStatus)
        {
            var result = new OrchestratorExecutionResult
            {
                CustomStatus = customStatus,
                Actions = actions,
            };

            this.SetResultInternal(result);
        }

        // TODO: This method should be considered deprecated because SDKs should no longer be returning results as JSON.
        internal void SetResult(string orchestratorResponseJsonText)
        {
            const string ActionsFieldName = nameof(OrchestratorExecutionResult.Actions);
            const string CustomStatusFieldName = nameof(OrchestratorExecutionResult.CustomStatus);

            Exception? jsonReaderException = null;
            OrchestratorExecutionResult? result = null;
            try
            {
                // Validate the JSON since we don't know for sure if the out-of-proc SDK is correctly implemented.
                // We validate the JObject directly instead of the deserialized object since .NET objects don't distinguish
                // between null and not present.
                JObject orchestratorResponseJson = JObject.Parse(orchestratorResponseJsonText);
                if (orchestratorResponseJson.TryGetValue(ActionsFieldName, StringComparison.OrdinalIgnoreCase, out _) &&
                    orchestratorResponseJson.TryGetValue(CustomStatusFieldName, StringComparison.OrdinalIgnoreCase, out _))
                {
                    result = orchestratorResponseJson.ToObject<OrchestratorExecutionResult>();
                }
            }
            catch (JsonReaderException e)
            {
                jsonReaderException = e;
            }

            if (result == null)
            {
                throw new ArgumentException(
                    message: "Unrecognized orchestrator response payload. The response is expected to be a JSON object with " +
                            $"a '{ActionsFieldName}' array property and a '{CustomStatusFieldName}' string property. " +
                            "Ensure that a compatible SDK is being used to generate the orchestrator response.",
                    paramName: nameof(orchestratorResponseJsonText),
                    innerException: jsonReaderException);
            }

            this.SetResultInternal(result);
        }

        private void SetResultInternal(OrchestratorExecutionResult result)
        {
            // Look for an orchestration completion action to see if we need to grab the output.
            foreach (OrchestratorAction action in result.Actions)
            {
                if (action is OrchestrationCompleteOrchestratorAction completeAction)
                {
                    this.OrchestratorCompleted = true;
                    this.SerializedOutput = completeAction.Result;
                    this.ContinuedAsNew = completeAction.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew;
                    break;
                }
            }

            this.executionResult = result;
        }

        internal OrchestratorExecutionResult GetResult()
        {
            return this.executionResult ?? throw new InvalidOperationException($"The execution result has not yet been set using {nameof(this.SetResult)}.");
        }
    }
}
