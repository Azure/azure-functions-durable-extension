// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// ETW Event Provider for the WebJobs.Extensions.DurableTask extension.
    /// </summary>
    [EventSource(Name = "WebJobs-Extensions-DurableTask")]
    internal sealed class EtwEventSource : EventSource
    {
        public static EtwEventSource Instance = new EtwEventSource();

        // Private .ctor - callers should use the shared static instance.
        private EtwEventSource()
        { }

        [Event(201, Level = EventLevel.Informational)]
        public void FunctionScheduled(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            string Reason,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(201, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, Reason, IsOrchestrator, IsReplay);
        }

        [Event(202, Level = EventLevel.Informational)]
        public void FunctionStarting(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            string Input,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(202, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, Input ?? "(null)", IsOrchestrator, IsReplay);
        }

        [Event(203, Level = EventLevel.Informational)]
        public void FunctionAwaited(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(203, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, IsOrchestrator, IsReplay);
        }

        [Event(204, Level = EventLevel.Informational)]
        public void FunctionListening(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            string Reason,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(204, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, Reason, IsOrchestrator, IsReplay);
        }

        [Event(205, Level = EventLevel.Informational)]
        public void ExternalEventRaised(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            string EventName,
            string Input,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(205, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, EventName, Input ?? "(null)", IsOrchestrator, IsReplay);
        }

        [Event(206, Level = EventLevel.Informational)]
        public void FunctionCompleted(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            string Output,
            bool ContinuedAsNew,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(206, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, Output ?? "(null)", ContinuedAsNew, IsOrchestrator, IsReplay);
        }

        [Event(207, Level = EventLevel.Warning)]
        public void FunctionTerminated(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            string Reason,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(207, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, Reason, IsOrchestrator, IsReplay);
        }

        [Event(208, Level = EventLevel.Error)]
        public void FunctionFailed(
            string HubName,
            string AppName,
            string SlotName,
            string FunctionName,
            string Version,
            string InstanceId,
            string Reason,
            bool IsOrchestrator,
            bool IsReplay)
        {
            this.WriteEvent(208, HubName, AppName, SlotName, FunctionName, Version ?? "", InstanceId, Reason, IsOrchestrator, IsReplay);
        }
    }
}
