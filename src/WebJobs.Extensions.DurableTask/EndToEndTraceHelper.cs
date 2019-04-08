// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EndToEndTraceHelper
    {
        private static readonly string ExtensionVersion = FileVersionInfo.GetVersionInfo(typeof(DurableTaskExtension).Assembly.Location).FileVersion;

        private static string appName;
        private static string slotName;

        private readonly ILogger logger;
        private readonly bool traceReplayEvents;

        private long sequenceNumber;

        public EndToEndTraceHelper(ILogger logger, bool traceReplayEvents)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.traceReplayEvents = traceReplayEvents;
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
            EtwEventSource.Instance.FunctionScheduled(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay))
            {
                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' scheduled. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, reason, isReplay, FunctionState.Scheduled, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void FunctionStarting(
            string hubName,
            string functionName,
            string instanceId,
            string operationId,
            string operationName,
            string input,
            FunctionType functionType,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionStarting(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                operationId,
                operationName,
                input,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay))
            {
                if (string.IsNullOrEmpty(operationName))
                {
                    this.logger.LogInformation(
                        "{instanceId}: Function '{functionName} ({functionType})' started. IsReplay: {isReplay}. Input: {input}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, isReplay, input, FunctionState.Started, hubName,
                        LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
                else
                {
                    this.logger.LogInformation(
                        "{instanceId}: Function '{functionName} ({functionType})' started '{operationName}' operation {operationId}. IsReplay: {isReplay}. Input: {input}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, operationName, operationId, isReplay, input, FunctionState.Started, hubName,
                        LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
            }
        }

        public void FunctionAwaited(
            string hubName,
            string functionName,
            FunctionType functionType,
            string instanceId,
            string operationId,
            string operationName,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionAwaited(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                operationId,
                operationName,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay))
            {
                if (string.IsNullOrEmpty(operationName))
                {
                    this.logger.LogInformation(
                        "{instanceId}: Function '{functionName} ({functionType})' awaited. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, isReplay, FunctionState.Awaited, hubName, LocalAppName,
                        LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
                else
                {
                    this.logger.LogInformation(
                        "{instanceId}: Function '{functionName} ({functionType})' awaited '{operationName}' operation {operationId}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, operationName, operationId, isReplay, FunctionState.Awaited, hubName, LocalAppName,
                        LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
            }
        }

        public void FunctionListening(
            string hubName,
            string functionName,
            string instanceId,
            string reason,
            bool isReplay)
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
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay))
            {
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
            string operationId,
            string operationName,
            string output,
            bool continuedAsNew,
            FunctionType functionType,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionCompleted(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                operationId,
                operationName,
                output,
                continuedAsNew,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay))
            {
                if (string.IsNullOrEmpty(operationName))
                {
                    this.logger.LogInformation(
                        "{instanceId}: Function '{functionName} ({functionType})' completed. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, continuedAsNew, isReplay, output, FunctionState.Completed,
                        hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
                else
                {
                    this.logger.LogInformation(
                        "{instanceId}: Function '{functionName} ({functionType})' completed '{operationName}' operation {operationId}. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, operationName, operationId, continuedAsNew, isReplay, output, FunctionState.Completed,
                        hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
            }
        }

        public void FunctionTerminated(
            string hubName,
            string functionName,
            string instanceId,
            string reason)
        {
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

            if (this.ShouldLogEvent(isReplay: false))
            {
                this.logger.LogWarning(
                    "{instanceId}: Function '{functionName} ({functionType})' was terminated. Reason: {reason}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, reason, FunctionState.Terminated, hubName, LocalAppName,
                    LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void FunctionRewound(
            string hubName,
            string functionName,
            string instanceId,
            string reason)
        {
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

            if (this.ShouldLogEvent(isReplay: false))
            {
                this.logger.LogWarning(
                    "{instanceId}: Function '{functionName} ({functionType})' was rewound. Reason: {reason}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, reason, FunctionState.Rewound, hubName, LocalAppName,
                    LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void FunctionFailed(
            string hubName,
            string functionName,
            string instanceId,
            string operationId,
            string operationName,
            string reason,
            FunctionType functionType,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionFailed(hubName, LocalAppName, LocalSlotName, functionName,
                instanceId, operationId, operationName, reason, functionType.ToString(), ExtensionVersion, isReplay);
            if (this.ShouldLogEvent(isReplay))
            {
                if (string.IsNullOrEmpty(operationName))
                {
                    this.logger.LogError(
                        "{instanceId}: Function '{functionName} ({functionType})' failed with an error. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, reason, isReplay, FunctionState.Failed, hubName,
                        LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
                else
                {
                    this.logger.LogError(
                        "{instanceId}: Function '{functionName} ({functionType})' failed '{operationName}' operation {operationId} with an error. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                        instanceId, functionName, functionType, operationName, operationId, reason, isReplay, FunctionState.Failed, hubName,
                        LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
                }
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
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay: false))
            {
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
            EtwEventSource.Instance.ExternalEventSaved(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                eventName,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay: false))
            {
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
            this.logger.LogInformation(
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
            this.logger.LogInformation(
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
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay: isReplay))
            {
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
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay: isReplay))
            {
                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' received an entity response. OperationId: {operationId}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, operationId, FunctionState.ExternalEventRaised, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        public void EntityLockAcquired(
            string hubName,
            string functionName,
            string instanceId,
            string requestingInstance,
            string requestId,
            bool isReplay)
        {
            FunctionType functionType = FunctionType.Entity;

            EtwEventSource.Instance.EntityLockAcquired(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                instanceId,
                requestingInstance,
                requestId,
                FunctionType.Entity.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay: isReplay))
            {
                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' granted lock to request {requestId} by instance {requestingInstance}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, requestId, requestingInstance, FunctionState.LockAcquired, hubName,
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
                IsReplay: isReplay);

            if (this.ShouldLogEvent(isReplay: isReplay))
            {
                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' released lock held by request {requestId} by instance {requestingInstance}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, requestId, requestingInstance, FunctionState.LockReleased, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
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
            bool isReplay = false;

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
                isReplay,
                latencyMs);

            if (this.ShouldLogEvent(isReplay: false))
            {
                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' sent a '{functionState}' notification event to Azure Event Grid. Status code: {statusCode}. Details: {details}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. Latency: {latencyMs} ms.",
                    instanceId, functionName, functionType, functionState, (int)statusCode, details, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, latencyMs);
            }
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
            bool isReplay = false;

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
                isReplay,
                latencyMs);

            if (this.ShouldLogEvent(isReplay: false))
            {
                this.logger.LogError(
                    "{instanceId}: Function '{functionName} ({functionType})' failed to send a '{functionState}' notification event to Azure Event Grid. Status code: {statusCode}. Details: {details}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. Latency: {latencyMs} ms.",
                    instanceId, functionName, functionType, functionState, (int)statusCode, details, hubName,
                    LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++, latencyMs);
            }
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
            bool isReplay = false;

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
                isReplay,
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
                IsReplay: isReplay);
            if (this.ShouldLogEvent(isReplay: false))
            {
                this.logger.LogInformation(
                    "{instanceId}: Function '{functionName} ({functionType})' was resumed by a timer scheduled for '{expirationTime}'. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                    instanceId, functionName, functionType, expirationTimeString, isReplay, FunctionState.TimerExpired,
                    hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
            }
        }

        private bool ShouldLogEvent(bool isReplay)
        {
            return this.traceReplayEvents || !isReplay;
        }
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
    }
}
