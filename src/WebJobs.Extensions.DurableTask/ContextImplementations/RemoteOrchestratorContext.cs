// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using DurableTask.Core;
using DurableTask.Core.Command;
using DurableTask.Core.Entities;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RemoteOrchestratorContext
    {
        private readonly OrchestrationRuntimeState runtimeState;

        private OrchestratorExecutionResult? executionResult;

        private Exception? failure;

        public RemoteOrchestratorContext(OrchestrationRuntimeState runtimeState, TaskOrchestrationEntityParameters? entityParameters, DurableTaskOptions options, bool isExtendedSession, bool includePastEvents)
        {
            this.runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
            this.EntityParameters = entityParameters;
            this.Configurations = new RemoteOrchestratorConfiguration
            {
                HttpDefaultAsyncRequestSleepTimeMilliseconds = options.HttpSettings.DefaultAsyncRequestSleepTimeMilliseconds,
            };
            if (options.ExtendedSessionsEnabled)
            {
                this.Configurations.IsExtendedSession = isExtendedSession;
                this.Configurations.IncludePastEvents = includePastEvents;
                this.Configurations.ExtendedSessionIdleTimeoutInSeconds = options.ExtendedSessionIdleTimeoutInSeconds;
            }
        }

        [JsonProperty("instanceId")]
        public string InstanceId => this.runtimeState.OrchestrationInstance?.InstanceId ?? string.Empty;

        [JsonProperty("pastEvents")]
        public IEnumerable<HistoryEvent> PastEvents => this.runtimeState.PastEvents;

        [JsonProperty("newEvents")]
        public IEnumerable<HistoryEvent> NewEvents => this.runtimeState.NewEvents;

        [JsonProperty("upperSchemaVersion")]
        internal int UpperSchemaVersion { get; } = 4;

        [JsonProperty("configurations")]
        public RemoteOrchestratorConfiguration Configurations { get; private set; }

        [JsonIgnore]
        internal bool OrchestratorCompleted { get; private set; }

        [JsonIgnore]
        internal bool ContinuedAsNew { get; private set; }

        [JsonIgnore]
        internal string? SerializedOutput { get; private set; }

        [JsonIgnore]
        internal TaskOrchestrationEntityParameters? EntityParameters { get; private set; }

        internal void ThrowIfFailed()
        {
            if (this.failure != null)
            {
                throw this.failure;
            }
        }

        internal OrchestratorExecutionResult GetResult()
        {
            return this.executionResult ?? throw new InvalidOperationException($"The execution result has not yet been set using {nameof(this.SetResult)}.");
        }

        internal bool TryGetOrchestrationErrorDetails(out Exception? failure)
        {
            bool hasError = this.failure != null;
            failure = hasError ? this.failure : null;
            return hasError;
        }

        internal void SetResult(IEnumerable<OrchestratorAction> actions, string customStatus)
        {
            ValidateCustomStatusSize(customStatus);

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

        private void ThrowIfPlatformLevelException(FailureDetails failureDetails)
        {
            // Recursively inspect the FailureDetails of the failed orchestrator and throw if a platform-level exception is detected.
            //
            // Today, this method only checks for <see cref="OutOfMemoryException"/>. In the future, we may want to add more cases.
            // Other known platform-level exceptions, like timeouts or process exists due to `Environment.FailFast`, do not yield
            // a `OrchestratorExecutionResult` as the isolated invocation is abruptly terminated. Therefore, they don't need to be
            // handled in this method.
            // However, our tests reveal that OOMs are, surprisngly, caught and returned as a `OrchestratorExecutionResult`
            // by the isolated process, and thus need special handling.
            //
            // It's unclear if all OOMs are caught by the isolated process (probably not), and also if there are other platform-level
            // errors that are also caught in the isolated process and returned as a `OrchestratorExecutionResult`. Let's add them
            // to this method as we encounter them.
            if (failureDetails.IsCausedBy<OutOfMemoryException>())
            {
                throw new SessionAbortedException(failureDetails.ErrorMessage);
            }

            if (failureDetails.InnerFailure != null)
            {
                this.ThrowIfPlatformLevelException(failureDetails.InnerFailure);
            }
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

                    if (completeAction.OrchestrationStatus == OrchestrationStatus.Failed)
                    {
                        // If the orchestrator failed due to a platform-level error in the isolated process,
                        // we should re-throw that exception in the host (this process) invocation pipeline,
                        // so the invocation can be retried.
                        if (completeAction.FailureDetails != null)
                        {
                            this.ThrowIfPlatformLevelException(completeAction.FailureDetails);
                        }

                        string message = completeAction switch
                        {
                            { FailureDetails: { } f } => f.ErrorMessage,
                            { Result: { } r } => r,
                            _ => "Exception occurred during orchestration execution.",
                        };

                        this.failure = new OrchestrationFailureException(message);
                    }

                    break;
                }
            }

            this.executionResult = result;
        }

        private static void ValidateCustomStatusSize(string customStatus)
        {
            // Azure Table Storage enforces a 32 KB limit if a property value is a UTF-16 encoded string.
            // We apply a 16 KB limit here to align with our in-process model.
            const int MaxCustomStatusSizeInKB = 16;
            double customStatusSizeInKB = customStatus != null ? Encoding.Unicode.GetByteCount(customStatus) / 1024.0 : 0;

            // If the provided custom status value exceeds 16KB in size, fail the orchestration.
            if (customStatusSizeInKB > MaxCustomStatusSizeInKB)
            {
                throw new ArgumentException(
                    $"CustomStatus is too large: limit = {MaxCustomStatusSizeInKB} KB (UTF-16), actual = {customStatusSizeInKB:N2} KB.");
            }
        }
    }
}
