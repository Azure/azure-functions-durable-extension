// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EndToEndTraceHelper
    {
        private static readonly string ExtensionVersion = FileVersionInfo.GetVersionInfo(typeof(DurableTaskExtension).Assembly.Location).FileVersion;

        private static string? appName;
        private static string? slotName;

        private readonly ILogger logger;
        private readonly bool traceReplayEvents;
        private readonly bool shouldCensor;

        private long sequenceNumber;

        public EndToEndTraceHelper(ILogger logger, bool traceReplayEvents, bool shouldCensor = false)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.traceReplayEvents = traceReplayEvents;
            this.shouldCensor = shouldCensor;
        }

        public static string LocalAppName
        {
            get
            {
                if (appName == null)
                {
                    appName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? string.Empty;
                }

                return appName;
            }
        }

        public static string LocalSlotName
        {
            get
            {
                if (slotName == null)
                {
                    slotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? string.Empty;
                }

                return slotName;
            }
        }

#pragma warning disable SA1117 // Parameters should be on same line or separate lines

        public void ExtensionInformationalEvent(
            string hubName,
            string instanceId,
            string functionName,
            string message,
            bool writeToUserLogs)
        {
            EtwEventSource.Instance.ExtensionInformationalEvent(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                message,
                ExtensionVersion);

            if (writeToUserLogs)
            {
                this.logger.LogInformation(
                    "{details}. InstanceId: {instanceId}. Function: {functionName}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    message, instanceId, functionName, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void ExtensionWarningEvent(string hubName, string functionName, string instanceId, string message)
        {
            EtwEventSource.Instance.ExtensionWarningEvent(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                message,
                ExtensionVersion);

            this.logger.LogWarning(
                "{details}. InstanceId: {instanceId}. Function: {functionName}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                message, instanceId, functionName, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionScheduled(
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            FunctionType functionType,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.FunctionScheduled(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    reason,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' scheduled. Reason: {reason}. IsReplay: {isReplay}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, reason, isReplay, FunctionState.Scheduled, OrchestrationRuntimeStatus.Pending, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void FunctionStarting(
            string hubName,
            string functionName,
            string instanceId,
            string? input,
            FunctionType functionType,
            bool isReplay,
            int taskEventId = -1)
        {

            if (this.shouldCensor)
            {
                input = "";
            }

            if (isReplay)
            {
                input = "(replay)";
            }

            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.FunctionStarting(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    taskEventId,
                    instanceId,
                    input,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' started. IsReplay: {isReplay}. Input: {input}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. TaskEventId: {taskEventId}",
                    instanceId, functionName, functionType, isReplay, input, FunctionState.Started, OrchestrationRuntimeStatus.Running, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, taskEventId);
            }
        }

        public void FunctionAwaited(
            string hubName,
            string functionName,
            FunctionType functionType,
            string instanceId,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.FunctionAwaited(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' awaited. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, isReplay, FunctionState.Awaited, hubName, LocalAppName,
                    LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void FunctionListening(
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Orchestrator;

                EtwEventSource.Instance.FunctionListening(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    reason,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' is waiting for input. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, reason, isReplay, FunctionState.Listening, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void FunctionCompleted(
            string hubName,
            string functionName,
            string instanceId,
            string output,
            bool continuedAsNew,
            FunctionType functionType,
            bool isReplay,
            int taskEventId = -1)
        {
            if (this.shouldCensor)
            {
                output = "";
            }

            if (isReplay)
            {
                output = "(replay)";
            }

            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.FunctionCompleted(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    taskEventId,
                    instanceId,
                    output,
                    continuedAsNew,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' completed. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. TaskEventId: {taskEventId}",
                    instanceId, functionName, functionType, continuedAsNew, isReplay, output, FunctionState.Completed, OrchestrationRuntimeStatus.Completed, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, taskEventId);
            }
        }

        public void FunctionTerminated(
            string hubName,
            string functionName,
            string instanceId,
            string reason)
        {
            if (this.shouldCensor)
            {
                reason = "";
            }

            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.FunctionTerminated(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: false);

            this.logger.LogWarning(
                "{instanceId}: Function '{functionName} ({functionType})' was terminated. Reason: {reason}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, reason, FunctionState.Terminated, OrchestrationRuntimeStatus.Terminated, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void SuspendingOrchestration(
            string hubName,
            string functionName,
            string instanceId,
            string reason)
        {
            if (this.shouldCensor)
            {
                reason = "";
            }

            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.SuspendingOrchestration(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: false);

            this.logger.LogInformation(
                "{instanceId}: Suspending function '{functionName} ({functionType})'. Reason: {reason}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, reason, FunctionState.Suspended, OrchestrationRuntimeStatus.Suspended, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void ResumingOrchestration(
            string hubName,
            string functionName,
            string instanceId,
            string reason)
        {
            if (this.shouldCensor)
            {
                reason = "";
            }

            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.ResumingOrchestration(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: false);

            this.logger.LogInformation(
                "{instanceId}: Resuming function '{functionName} ({functionType})'. Reason: {reason}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, reason, FunctionState.Scheduled, OrchestrationRuntimeStatus.Running, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionRewound(
            string hubName,
            string functionName,
            string instanceId,
            string reason)
        {
            if (this.shouldCensor)
            {
                reason = "";
            }

            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.FunctionRewound(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: false);

            this.logger.LogWarning(
                "{instanceId}: Function '{functionName} ({functionType})' was rewound. Reason: {reason}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, reason, FunctionState.Rewound, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionFailed(
            string hubName,
            string functionName,
            string instanceId,
            Exception exception,
            FunctionType functionType,
            bool isReplay,
            int taskEventId = -1)
        {

            string reason = exception.Message;
            if (exception is OrchestrationFailureException orchestrationFailureException)
            {
                reason = orchestrationFailureException.Details;
            }

            string sanitizedReason = $"{exception.GetType().FullName}\n{exception.StackTrace}";

            if (isReplay)
            {
                reason = $"(replayed {exception.GetType().Name})";
                sanitizedReason = reason;
            }
            this.FunctionFailed(hubName, functionName, instanceId, reason, sanitizedReason, functionType, isReplay, taskEventId);
        }

        public void FunctionFailed(
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            string sanitizedReason,
            FunctionType functionType,
            bool isReplay,
            int taskEventId = -1)
        {

            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.FunctionFailed(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    taskEventId,
                    instanceId,
                    sanitizedReason,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogError(
                    "{instanceId}: Function '{functionName} ({functionType})' failed with an error. Reason: {reason}. IsReplay: {isReplay}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. TaskEventId: {taskEventId}",
                    instanceId, functionName, functionType, reason, isReplay, FunctionState.Failed, OrchestrationRuntimeStatus.Failed, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, taskEventId);
            }
        }

        public void FunctionAborted(
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            FunctionType functionType)
        {
            EtwEventSource.Instance.FunctionAborted(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: false);

            this.logger.LogWarning(
                "{instanceId}: Function '{functionName} ({functionType})' was aborted. Reason: {reason}. IsReplay: {isReplay}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, reason, false /*isReplay*/, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void OperationCompleted(
           string hubName,
           string functionName,
           string instanceId,
           string operationId,
           string operationName,
           string input,
           string output,
           double duration,
           bool isReplay)
        {
            if (this.shouldCensor)
            {
                input = "";
                output = "";
            }

            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.OperationCompleted(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    operationId,
                    operationName,
                    input,
                    output,
                    duration,
                    FunctionType.Entity.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' completed '{operationName}' operation {operationId} in {duration}ms. IsReplay: {isReplay}. Input: {input}. Output: {output}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, FunctionType.Entity, operationName, operationId, duration, isReplay, input, output,
                    hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void OperationFailed(
           string hubName,
           string functionName,
           string instanceId,
           string operationId,
           string operationName,
           string input,
           Exception exception,
           double duration,
           bool isReplay)
        {
            string exceptionString = exception.ToString();
            if (isReplay)
            {
                exceptionString = $"(replayed {exception.GetType().Name})";
            }

            this.OperationFailed(hubName, functionName, instanceId, operationId, operationName, input, exceptionString, duration, isReplay);
        }

        public void OperationFailed(
           string hubName,
           string functionName,
           string instanceId,
           string operationId,
           string operationName,
           string input,
           string exception,
           double duration,
           bool isReplay)
        {
            if (this.shouldCensor)
            {
                input = "";
                exception = "";
            }

            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.OperationFailed(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    operationId,
                    operationName,
                    input,
                    exception,
                    duration,
                    FunctionType.Entity.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogError(
                    "{instanceId}: Function '{functionName} ({functionType})' failed '{operationName}' operation {operationId} after {duration}ms with exception {exception}. Input: {input}. IsReplay: {isReplay}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, FunctionType.Entity, operationName, operationId, duration, exception, input, isReplay, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void ExternalEventRaised(
            string hubName,
            string functionName,
            string instanceId,
            string eventName,
            string input,
            bool isReplay)
        {
            if (this.shouldCensor)
            {
                input = "";
            }

            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Orchestrator;

                EtwEventSource.Instance.ExternalEventRaised(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    eventName,
                    input,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' received a '{eventName}' event. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, eventName, FunctionState.ExternalEventRaised, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void ExternalEventSaved(
            string hubName,
            string functionName,
            FunctionType functionType,
            string instanceId,
            string eventName,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.ExternalEventSaved(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    eventName,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' saved a '{eventName}' event to an in-memory queue. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, eventName, FunctionState.ExternalEventDropped, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public void DeliveringEntityMessage(
            string instanceId,
            string executionId,
            int eventId,
            string eventName,
            object eventContent)
        {
            this.logger.LogDebug(
                "{instanceId}: delivering message: {eventName} {eventContent} EventId: {eventId} ExecutionId: {executionId} SequenceNumber: {sequenceNumber}.",
                instanceId, eventName, eventContent, eventId, executionId, this.sequenceNumber++);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public void SendingEntityMessage(
            string instanceId,
            string executionId,
            string targetInstanceId,
            string eventName,
            object eventContent)
        {
            this.logger.LogDebug(
                "{instanceId}: sending message: {eventName} {eventContent}  TargetInstanceId: {targetInstanceId} ExecutionId: {executionId} SequenceNumber: {sequenceNumber}.",
                instanceId, eventName, eventContent, targetInstanceId, executionId, this.sequenceNumber++);
        }

        public void EntityOperationQueued(
            string hubName,
            string functionName,
            string instanceId,
            string operationId,
            string operationName,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Entity;

                EtwEventSource.Instance.EntityOperationQueued(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    operationId,
                    operationName,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' queued '{operationName}' operation {operationId}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, operationName, operationId, FunctionState.ExternalEventRaised, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void EntityResponseReceived(
            string hubName,
            string functionName,
            FunctionType functionType,
            string instanceId,
            string operationId,
            string result,
            bool isReplay)
        {
            if (this.shouldCensor)
            {
                result = "";
            }

            if (this.ShouldLogEvent(isReplay))
            {
                EtwEventSource.Instance.EntityResponseReceived(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    operationId,
                    result,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' received an entity response. OperationId: {operationId}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, operationId, FunctionState.ExternalEventRaised, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void EntityStateCreated(
            string hubName,
            string functionName,
            string instanceId,
            string operationName,
            string operationId,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Entity;

                EtwEventSource.Instance.EntityStateCreated(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    operationName,
                    operationId,
                    FunctionType.Entity.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' created entity state in '{operationName}' operation {operationId}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, operationName, operationId, FunctionState.EntityStateCreated, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void EntityStateDeleted(
            string hubName,
            string functionName,
            string instanceId,
            string operationName,
            string operationId,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Entity;

                EtwEventSource.Instance.EntityStateDeleted(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    operationName,
                    operationId,
                    FunctionType.Entity.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' deleted entity state in '{operationName}' operation {operationId}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, operationName, operationId, FunctionState.EntityStateDeleted, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void EntityLockAcquired(
            string hubName,
            string functionName,
            string instanceId,
            string requestingInstanceId,
            string requestingExecutionId,
            string requestId,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Entity;

                EtwEventSource.Instance.EntityLockAcquired(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    requestingInstanceId,
                    requestingExecutionId,
                    requestId,
                    FunctionType.Entity.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' granted lock to request {requestId} by instance {requestingInstanceId}, execution {requestingExecutionId}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, requestId, requestingInstanceId, requestingExecutionId, FunctionState.LockAcquired, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void EntityLockReleased(
            string hubName,
            string functionName,
            string instanceId,
            string requestingInstance,
            string requestId,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Entity;

                EtwEventSource.Instance.EntityLockReleased(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    requestingInstance,
                    requestId,
                    FunctionType.Entity.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' released lock held by request {requestId} by instance {requestingInstance}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, requestId, requestingInstance, FunctionState.LockReleased, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void EntityBatchCompleted(
            string hubName,
            string functionName,
            string instanceId,
            int eventsReceived,
            int operationsInBatch,
            int operationsExecuted,
            int? outOfOrderMessages,
            int queuedMessages,
            int userStateSize,
            int? sources,
            int? destinations,
            string lockedBy,
            bool suspended,
            string traceFlags)
        {
            FunctionType functionType = FunctionType.Entity;

            EtwEventSource.Instance.EntityBatchCompleted(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                eventsReceived,
                operationsInBatch,
                operationsExecuted,
                outOfOrderMessages?.ToString() ?? "",
                queuedMessages,
                userStateSize,
                sources?.ToString() ?? "",
                destinations?.ToString() ?? "",
                lockedBy ?? "",
                suspended,
                traceFlags,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: false);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})' received {eventsReceived} events and processed {operationsExecuted}/{operationsInBatch} entity operations. OutOfOrderMessages: {outOfOrderMessages}. QueuedMessages: {queuedMessages}. UserStateSize: {userStateSize}. Sources: {sources}. Destinations: {destinations}. LockedBy: {lockedBy}. Suspended: {suspended}. TraceFlags: {traceFlags}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType,
                eventsReceived, operationsExecuted, operationsInBatch, outOfOrderMessages, queuedMessages, userStateSize, sources, destinations, lockedBy, suspended, traceFlags,
                FunctionState.EntityBatch, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void EntityBatchFailed(
            string hubName,
            string functionName,
            string instanceId,
            string traceFlags,
            Exception error)
        {
            FunctionType functionType = FunctionType.Entity;
            string details = Utils.IsFatal(error) ? error.GetType().Name : error.ToString();
            string sanitizedDetails = $"{error.GetType().FullName}\n{error.StackTrace}";

            EtwEventSource.Instance.EntityBatchFailed(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                traceFlags,
                sanitizedDetails,
                functionType.ToString(),
                ExtensionVersion);

            this.logger.LogError(
                "{instanceId}: Function '{functionName} ({functionType})' failed. TraceFlags: {traceFlags}. Details: {details}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType,
                traceFlags, details,
                hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void EventGridSuccess(
            string hubName,
            string functionName,
            FunctionState functionState,
            string instanceId,
            string details,
            HttpStatusCode statusCode,
            string reason,
            long latencyMs)
        {
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.EventGridNotificationCompleted(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                functionState,
                instanceId,
                details,
                (int)statusCode,
                reason,
                functionType,
                ExtensionVersion,
                IsReplay: false,
                latencyMs);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})' sent a '{functionState}' notification event to Azure Event Grid. Status code: {statusCode}. Details: {details}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. Latency: {latencyMs} ms.",
                instanceId, functionName, functionType, functionState, (int)statusCode, details, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, latencyMs);
        }

        public void EventGridFailed(
            string hubName,
            string functionName,
            FunctionState functionState,
            string instanceId,
            string details,
            HttpStatusCode statusCode,
            string reason,
            long latencyMs)
        {
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.EventGridNotificationFailed(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                functionState,
                instanceId,
                details,
                (int)statusCode,
                reason,
                functionType,
                ExtensionVersion,
                IsReplay: false,
                latencyMs);

            this.logger.LogError(
                "{instanceId}: Function '{functionName} ({functionType})' failed to send a '{functionState}' notification event to Azure Event Grid. Status code: {statusCode}. Details: {details}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. Latency: {latencyMs} ms.",
                instanceId, functionName, functionType, functionState, (int)statusCode, details, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, latencyMs);
        }

        public void EventGridException(
            string hubName,
            string functionName,
            FunctionState functionState,
            string instanceId,
            string details,
            Exception exception,
            string reason,
            long latencyMs)
        {
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.EventGridNotificationException(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                functionState,
                ExtensionVersion,
                instanceId,
                details,
                exception.Message,
                reason,
                functionType,
                ExtensionVersion,
                IsReplay: false,
                latencyMs);

            this.logger.LogError(
                "{instanceId}: Function '{functionName} ({functionType})', failed to send a '{functionState}' notification event to Azure Event Grid. Exception message: {exceptionMessage}. Details: {details}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. Latency: {latencyMs} ms.",
                instanceId, functionName, functionType, functionState, exception.Message, details, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, latencyMs);
        }

        public void TimerExpired(
            string hubName,
            string functionName,
            string instanceId,
            DateTime expirationTime,
            bool isReplay)
        {
            if (this.ShouldLogEvent(isReplay))
            {
                FunctionType functionType = FunctionType.Orchestrator;

                string expirationTimeString = expirationTime.ToString("o");

                EtwEventSource.Instance.TimerExpired(
                    hubName,
                    LocalAppName,
                    LocalSlotName,
                    functionName,
                    instanceId,
                    expirationTimeString,
                    functionType.ToString(),
                    ExtensionVersion,
                    isReplay);

                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' was resumed by a timer scheduled for '{expirationTime}'. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, expirationTimeString, isReplay, FunctionState.TimerExpired, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void TraceConfiguration(
            string hubName,
            string configurationJsonString)
        {
            EtwEventSource.Instance.ExtensionConfiguration(
                hubName,
                LocalAppName,
                LocalSlotName,
                configurationJsonString,
                ExtensionVersion);

            this.logger.LogInformation(
                "Durable extension configuration loaded: {details}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                configurationJsonString, hubName, LocalAppName, LocalSlotName, ExtensionVersion);
        }

        public void RetrievingToken(
            string hubName,
            string resource)
        {
            EtwEventSource.Instance.RetrievingToken(
                hubName,
                LocalAppName,
                LocalSlotName,
                resource,
                ExtensionVersion);

            this.logger.LogInformation(
                "Attempting to retrieve authentication token for resource '{resource}'. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                resource, hubName, LocalAppName, LocalSlotName, ExtensionVersion);
        }

        public void TokenRetrievalFailed(
            string hubName,
            string resource,
            Exception exception)
        {
            EtwEventSource.Instance.TokenRetrievalFailed(
                hubName,
                LocalAppName,
                LocalSlotName,
                resource,
                exception.Message,
                ExtensionVersion);

            this.logger.LogError(
                default,
                exception,
                "Unable to retrieve authentication token for resource '{resource}'. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                resource, hubName, LocalAppName, LocalSlotName, ExtensionVersion);
        }

        public void TokenRenewalFailed(
            string hubName,
            string resource,
            int attempt,
            TimeSpan delay,
            Exception exception)
        {
            long delayMs = (long)delay.TotalMilliseconds;
            EtwEventSource.Instance.TokenRenewalFailed(
                hubName,
                LocalAppName,
                LocalSlotName,
                resource,
                attempt,
                delayMs,
                exception.Message,
                ExtensionVersion);

            this.logger.LogWarning(
                default,
                exception,
                "Unable to renew authentication token for resource '{resource}' on attempt #{attempt}. Will retry in {delay} ms. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                resource, attempt, delayMs, hubName, LocalAppName, LocalSlotName, ExtensionVersion);
        }

        private bool ShouldLogEvent(bool isReplay)
        {
            return this.traceReplayEvents || !isReplay;
        }
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
    }
}
