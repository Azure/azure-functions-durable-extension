// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    public class EndToEndTraceHelper
    {
        private const string CategoryName = "Host.Triggers.DurableTask";
        private static string appName;
        private static string slotName;
        private static readonly string ExtensionVersion = FileVersionInfo.GetVersionInfo(typeof(DurableTaskExtension).Assembly.Location).FileVersion;

        private readonly TraceWriter appTraceWriter;
        private readonly ILogger logger;

        public EndToEndTraceHelper(JobHostConfiguration config, TraceWriter traceWriter)
        {
            this.appTraceWriter = traceWriter;
            this.logger = config.LoggerFactory?.CreateLogger(CategoryName);
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
            FunctionType functionType,
            bool isReplay)
        {
            EtwEventSource.Instance.FunctionScheduled(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, reason, functionType.ToString(), ExtensionVersion, IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' scheduled. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, reason, isReplay, FunctionState.Scheduled, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' scheduled. Reason: {reason}. IsReplay: {isReplay}. State: {FunctionState.Scheduled}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
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
            EtwEventSource.Instance.FunctionStarting(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, input, functionType.ToString(), ExtensionVersion, IsReplay: isReplay);
            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' started. IsReplay: {isReplay}. Input: {input}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, isReplay, input, FunctionState.Started, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' started. IsReplay: {isReplay}. Input: {input}. State: {FunctionState.Started}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
        }

        public void FunctionAwaited(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            bool isReplay)
        {
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.FunctionAwaited(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, functionType.ToString(), ExtensionVersion, IsReplay: isReplay);
            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' awaited. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, isReplay, FunctionState.Awaited, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' awaited. IsReplay: {isReplay}. State: {FunctionState.Awaited}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
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

            EtwEventSource.Instance.FunctionListening(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, reason, functionType.ToString(), ExtensionVersion, IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' is waiting for input. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, reason, isReplay, FunctionState.Listening, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' is waiting for input. Reason: {reason}. IsReplay: {isReplay}. State: {FunctionState.Listening}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}.  ExtensionVersion: {ExtensionVersion}.");
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
            EtwEventSource.Instance.FunctionCompleted(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, output, continuedAsNew, functionType.ToString(), ExtensionVersion, IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' completed. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, continuedAsNew, isReplay, output,
                FunctionState.Completed, hubName, LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' completed. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}. State: {FunctionState.Completed}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
        }

        public void FunctionTerminated(
            string hubName,
            string functionName,
            string version,
            string instanceId,
            string reason)
        {
            FunctionType functionType = FunctionType.Orchestrator;

            EtwEventSource.Instance.FunctionTerminated(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, reason, functionType.ToString(), ExtensionVersion, IsReplay: false);

            this.logger.LogWarning(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' was terminated. Reason: {reason}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, reason, FunctionState.Terminated, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' was terminated. Reason: {reason}. State: {FunctionState.Terminated}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
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
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' failed with an error. Reason: {reason}. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, reason, isReplay, FunctionState.Failed, hubName,
                LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' failed with an error. Reason: {reason}. IsReplay: {isReplay}. State: {FunctionState.Failed}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
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

            EtwEventSource.Instance.ExternalEventRaised(hubName, LocalAppName, LocalSlotName, functionName, version,
                instanceId, eventName, input, functionType.ToString(), ExtensionVersion, IsReplay: isReplay);

            this.logger.LogInformation(
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' received a '{eventName}' event. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, eventName, FunctionState.ExternalEventRaised,
                hubName, LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' received a '{eventName}' event. State: {FunctionState.ExternalEventRaised}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
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
                "{instanceId}: Function '{functionName} ({functionType})', version '{version}' was resumed by a timer scheduled for '{expirationTime}'. IsReplay: {isReplay}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}.",
                instanceId, functionName, functionType, version, expirationTimeString, isReplay, FunctionState.TimerExpired,
                hubName, LocalAppName, LocalSlotName, ExtensionVersion);
            this.appTraceWriter.Info(
                $"{instanceId}: Function '{functionName} ({functionType})', version '{version}' was resumed by a timer scheduled for '{expirationTimeString}'. IsReplay: {isReplay}. State: {FunctionState.TimerExpired}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {ExtensionVersion}.");
        }
    }
}
