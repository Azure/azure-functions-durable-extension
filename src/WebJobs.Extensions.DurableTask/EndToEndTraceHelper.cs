// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EndToEndTraceHelper
    {
        private static string appName;
        private static string slotName;

        // TODO: Replace TraceWriter with structured event client
        private readonly TraceWriter appTraceWriter;

        public EndToEndTraceHelper(TraceWriter appTraceWriter)
        {
            this.appTraceWriter = appTraceWriter;
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

        public void FunctionScheduled(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason,
            bool isOrchestrator,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionScheduled(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, reason, isOrchestrator, isReplay);
            this.appTraceWriter.Info($"[DF] {instanceId}: Scheduling function '{functionName}', version '{version}'. reason: {reason}. IsReplay: {isReplay}.");
        }

        public void FunctionStarting(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string input,
            bool isOrchestrator,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionStarting(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, input, isOrchestrator, isReplay);
            this.appTraceWriter.Info($"[DF] {instanceId}: Starting function '{functionName}', version '{version}'. IsReplay: {isReplay}. Input: {input}");
        }

        public void FunctionAwaited(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionAwaited(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, IsOrchestrator: true, IsReplay: isReplay);
            this.appTraceWriter.Info($"[DF] {instanceId}: Function '{functionName}', version '{version}' awaited. IsReplay: {isReplay}.");
        }

        public void FunctionListening(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionListening(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, reason, IsOrchestrator: true, IsReplay: isReplay);
            this.appTraceWriter.Info($"[DF] {instanceId}: Function '{functionName}', version '{version}' is waiting for input. Reason: {reason}. IsReplay: {isReplay}.");
        }

        public void FunctionCompleted(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string output,
            bool continuedAsNew,
            bool isOrchestrator,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionCompleted(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, output, continuedAsNew, isOrchestrator, isReplay);
            this.appTraceWriter.Info($"[DF] {instanceId}: Function '{functionName}', version '{version}' completed. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}");
        }

        public void FunctionTerminated(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason)
        {
            EtwEventSource.Instance.FunctionTerminated(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, reason, IsOrchestrator: true, IsReplay: false);
            this.appTraceWriter.Warning($"[DF] {instanceId}: Function '{functionName}', version '{version}' was terminated. Reason: {reason}");
        }

        public void FunctionFailed(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason,
            bool isOrchestrator,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionFailed(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, reason, isOrchestrator, IsReplay: false);
            this.appTraceWriter.Error($"[DF] {instanceId}: Function '{functionName}', version '{version}' failed with an error. Reason: {reason}. IsReplay: {isReplay}.");
        }

        public void ExternalEventRaised(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string eventName,
            string input,
            bool isReplay)
        {
            EtwEventSource.Instance.ExternalEventRaised(hubName, LocalAppName, LocalSlotName, functionName, version, instanceId, eventName, input, IsOrchestrator: true, IsReplay: isReplay);
            this.appTraceWriter.Info($"[DF] {instanceId}: Function '{functionName}', version '{version}' received a '{eventName}' event.");
        }
    }
}
