// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// ETW Event Provider for the WebJobs.Extensions.DurableTask extension.
    /// </summary>
    [EventSource(Name = "WebJobs-Extensions-DurableTask")]
    internal sealed class EtwEventSource : EventSource
    {
        public static readonly EtwEventSource Instance = new EtwEventSource();

        // Private .ctor - callers should use the shared static instance.
        private EtwEventSource()
        { }

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter

        [Event(201, Level = EventLevel.Informational, Version = 2)]
        public void FunctionScheduled(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Reason,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(201, TaskHub, AppName, SlotName, FunctionName, InstanceId, Reason, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(202, Level = EventLevel.Informational, Version = 5)]
        public void FunctionStarting(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            int TaskEventId,
            string InstanceId,
            string Input,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(202, TaskHub, AppName, SlotName, FunctionName, TaskEventId, InstanceId, Input ?? "(null)", FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(203, Level = EventLevel.Informational, Version = 4)]
        public void FunctionAwaited(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(203, TaskHub, AppName, SlotName, FunctionName, InstanceId, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(204, Level = EventLevel.Informational, Version = 2)]
        public void FunctionListening(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Reason,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(204, TaskHub, AppName, SlotName, FunctionName, InstanceId, Reason, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(205, Level = EventLevel.Informational, Version = 2)]
        public void ExternalEventRaised(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string EventName,
            string Input,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(205, TaskHub, AppName, SlotName, FunctionName, InstanceId, EventName, Input ?? "(null)", FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(206, Level = EventLevel.Informational, Version = 5)]
        public void FunctionCompleted(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            int TaskEventId,
            string InstanceId,
            string Output,
            bool ContinuedAsNew,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(206, TaskHub, AppName, SlotName, FunctionName, TaskEventId, InstanceId, Output ?? "(null)", ContinuedAsNew, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(207, Level = EventLevel.Warning, Version = 2)]
        public void FunctionTerminated(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Reason,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(207, TaskHub, AppName, SlotName, FunctionName, InstanceId, Reason, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(208, Level = EventLevel.Error, Version = 4)]
        public void FunctionFailed(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            int TaskEventId,
            string InstanceId,
            string Reason,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(208, TaskHub, AppName, SlotName, FunctionName, TaskEventId, InstanceId, Reason, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(209, Level = EventLevel.Informational, Version = 2)]
        public void TimerExpired(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Reason,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(209, TaskHub, AppName, SlotName, FunctionName, InstanceId, Reason, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(210, Level = EventLevel.Informational, Version = 3)]
        public void EventGridNotificationCompleted(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            FunctionState FunctionState,
            string InstanceId,
            string Details,
            int StatusCode,
            string Reason,
            FunctionType FunctionType,
            string ExtensionVersion,
            bool IsReplay,
            long LatencyMs)
        {
            this.WriteEvent(210, TaskHub, AppName, SlotName, FunctionName, FunctionState, InstanceId, Details, StatusCode, Reason, FunctionType, ExtensionVersion, IsReplay, LatencyMs);
        }

        [Event(211, Level = EventLevel.Error, Version = 3)]
        public void EventGridNotificationFailed(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            FunctionState FunctionState,
            string InstanceId,
            string Details,
            int StatusCode,
            string Reason,
            FunctionType FunctionType,
            string ExtensionVersion,
            bool IsReplay,
            long LatencyMs)
        {
            this.WriteEvent(211, TaskHub, AppName, SlotName, FunctionName, FunctionState, InstanceId, Details, StatusCode, Reason, FunctionType, ExtensionVersion, IsReplay, LatencyMs);
        }

        [Event(212, Level = EventLevel.Error)]
        public void EventGridNotificationException(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            FunctionState FunctionState,
            string Version,
            string InstanceId,
            string Details,
            string Reason,
            string exceptionMessage,
            FunctionType FunctionType,
            string ExtensionVersion,
            bool IsReplay,
            long LatencyMs)
        {
            this.WriteEvent(212, TaskHub, AppName, SlotName, FunctionName, FunctionState, Version ?? "", InstanceId, Details, exceptionMessage, Reason, FunctionType, ExtensionVersion, IsReplay, LatencyMs);
        }

        [Event(213, Level = EventLevel.Informational)]
        public void ExtensionInformationalEvent(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Details,
            string ExtensionVersion)
        {
            this.WriteEvent(213, TaskHub, AppName, SlotName, FunctionName ?? string.Empty, InstanceId ?? string.Empty, Details, ExtensionVersion);
        }

        [Event(214, Level = EventLevel.Warning)]
        public void ExtensionWarningEvent(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Details,
            string ExtensionVersion)
        {
            this.WriteEvent(214, TaskHub, AppName, SlotName, FunctionName ?? string.Empty, InstanceId ?? string.Empty, Details, ExtensionVersion);
        }

        [Event(215, Level = EventLevel.Informational, Version = 2)]
        public void ExternalEventSaved(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string EventName,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(215, TaskHub, AppName, SlotName, FunctionName, InstanceId, EventName, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(216, Level = EventLevel.Informational)]
        public void FunctionRewound(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Reason,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(216, TaskHub, AppName, SlotName, FunctionName, InstanceId, Reason ?? string.Empty, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(217, Level = EventLevel.Informational)]
        public void EntityOperationQueued(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string OperationId,
            string OperationName,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(217, TaskHub, AppName, SlotName, FunctionName, InstanceId, OperationId, OperationName, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(218, Level = EventLevel.Informational)]
        public void EntityResponseReceived(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string OperationId,
            string Result,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(218, TaskHub, AppName, SlotName, FunctionName, InstanceId, OperationId, Result ?? "(null)", FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(219, Level = EventLevel.Informational, Version = 2)]
        public void EntityLockAcquired(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string RequestingInstanceId,
            string RequestingExecutionId,
            string RequestId,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(219, TaskHub, AppName, SlotName, FunctionName, InstanceId, RequestingInstanceId, RequestingExecutionId ?? "", RequestId, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(220, Level = EventLevel.Informational)]
        public void EntityLockReleased(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string RequestingInstance,
            string RequestId,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(220, TaskHub, AppName, SlotName, FunctionName, InstanceId, RequestingInstance, RequestId, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(221, Level = EventLevel.Informational)]
        public void OperationCompleted(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string OperationId,
            string OperationName,
            string Input,
            string Output,
            double Duration,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(221, TaskHub, AppName, SlotName, FunctionName, InstanceId, OperationId, OperationName, Input ?? "(null)", Output ?? "(null)", Duration, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(222, Level = EventLevel.Error)]
        public void OperationFailed(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string OperationId,
            string OperationName,
            string Input,
            string Exception,
            double Duration,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(222, TaskHub, AppName, SlotName, FunctionName, InstanceId, OperationId, OperationName, Input ?? "(null)", Exception, Duration, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(223, Level = EventLevel.Informational)]
        public void ExtensionConfiguration(
            string TaskHub,
            string AppName,
            string SlotName,
            string Details,
            string ExtensionVersion)
        {
            this.WriteEvent(223, TaskHub, AppName, SlotName, Details, ExtensionVersion);
        }

        [Event(224, Level = EventLevel.Warning)]
        public void FunctionAborted(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Reason,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(224, TaskHub, AppName, SlotName, FunctionName, InstanceId, Reason, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(225, Level = EventLevel.Informational)]
        public void EntityStateCreated(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string OperationName,
            string OperationId,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(225, TaskHub, AppName, SlotName, FunctionName, InstanceId, OperationName, OperationId, FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(226, Level = EventLevel.Informational)]
        public void EntityStateDeleted(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string OperationName,
            string OperationId,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(226, TaskHub, AppName, SlotName, FunctionName, InstanceId, OperationName, OperationId, FunctionType, ExtensionVersion, IsReplay);
        }

#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    }
}
