// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Tracing;
using System.Net;

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

        [Event(202, Level = EventLevel.Informational, Version = 2)]
        public void FunctionStarting(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Input,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(202, TaskHub, AppName, SlotName, FunctionName, InstanceId, Input ?? "(null)", FunctionType, ExtensionVersion, IsReplay);
        }

        [Event(203, Level = EventLevel.Informational, Version = 2)]
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

        [Event(206, Level = EventLevel.Informational, Version = 2)]
        public void FunctionCompleted(
            string TaskHub,
            string AppName,
            string SlotName,
            string FunctionName,
            string InstanceId,
            string Output,
            bool ContinuedAsNew,
            string FunctionType,
            string ExtensionVersion,
            bool IsReplay)
        {
            this.WriteEvent(206, TaskHub, AppName, SlotName, FunctionName, InstanceId, Output ?? "(null)", ContinuedAsNew, FunctionType, ExtensionVersion, IsReplay);
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

        [Event(208, Level = EventLevel.Error, Version = 2)]
        public void FunctionFailed(
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
            this.WriteEvent(208, TaskHub, AppName, SlotName, FunctionName, InstanceId, Reason, FunctionType, ExtensionVersion, IsReplay);
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

#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    }
}
