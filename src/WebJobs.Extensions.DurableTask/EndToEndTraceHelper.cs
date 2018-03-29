// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EndToEndTraceHelper
    {
        private static readonly string ExtensionVersion = FileVersionInfo.GetVersionInfo(typeof(DurableTaskExtension).Assembly.Location).FileVersion;

        private static string appName;
        private static string slotName;

        private readonly ILogger logger;

        private long sequenceNumber;

        public EndToEndTraceHelper(JobHostConfiguration config, ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        public void FunctionScheduled(
            string hubName,
            string functionName,
            string version,
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
                version,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' scheduled. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, reason, isReplay, FunctionState.Scheduled, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionStarting(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string input,
            FunctionType functionType,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionStarting(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                version,
                instanceId,
                input,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' started. IsReplay: {isReplay}. Input: {input}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, isReplay, input, FunctionState.Started, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionAwaited(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            bool isReplay)
        {
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.FunctionAwaited(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                version,
                instanceId,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' awaited. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, isReplay, FunctionState.Awaited, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionListening(
            string hubName,
            string functionName,
            string version,
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
                version,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' is waiting for input. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, reason, isReplay, FunctionState.Listening, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionCompleted(
            string hubName,
            string functionName,
            string version,
            string instanceId,
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
                version,
                instanceId,
                output,
                continuedAsNew,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' completed. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, continuedAsNew, isReplay, output, FunctionState.Completed, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionTerminated(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason)
        {
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.FunctionTerminated(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                version,
                instanceId,
                reason,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: false);

            this.logger.LogWarning(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' was terminated. Reason: {reason}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, reason, FunctionState.Terminated, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void FunctionFailed(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason,
            FunctionType functionType,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionFailed(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, reason, functionType.ToString(), ExtensionVersion, IsReplay: false);

            this.logger.LogError(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' failed with an error. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, reason, isReplay, FunctionState.Failed, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
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
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.ExternalEventRaised(
                hubName,
                LocalAppName,
                LocalSlotName,
                functionName,
                version,
                instanceId,
                eventName,
                input,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' received a '{eventName}' event. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, eventName, FunctionState.ExternalEventRaised, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }

        public void TimerExpired(
            string hubName,
            string functionName,
            string version,
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
                version,
                instanceId,
                expirationTimeString,
                functionType.ToString(),
                ExtensionVersion,
                IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' was resumed by a timer scheduled for '{expirationTime}'. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.",
                instanceId, functionName, functionType, version, expirationTimeString, isReplay, FunctionState.TimerExpired, hubName, LocalAppName, LocalSlotName, ExtensionVersion, this.sequenceNumber++);
        }
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
    }
}
