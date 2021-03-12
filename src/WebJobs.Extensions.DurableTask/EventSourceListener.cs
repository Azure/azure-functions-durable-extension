// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// An EventSource Listener. It sets up a callback for internal EventSource events
    /// that we use to capture their data and log it in Linux App Service plans.
    /// </summary>
    internal class EventSourceListener : EventListener
    {
        private readonly LinuxAppServiceLogger logger;
        private readonly bool disableVerbose;
        private readonly string durabilityProviderEventSourceName;
        private EndToEndTraceHelper traceHelper;

        /// <summary>
        /// Create an EventSourceListener to capture and log Durable EventSource
        /// data in Linux.
        /// </summary>
        /// <param name="logger">A LinuxAppService logger configured for the current linux host.</param>
        /// <param name="enableVerbose">If true, durableTask.Core verbose logs are enabled. The opposite if false.</param>
        /// <param name="traceHelper">A tracing client to log exceptions.</param>
        /// <param name="durabilityProviderEventSourceName">The durability provider's event source name.</param>
        public EventSourceListener(LinuxAppServiceLogger logger, bool enableVerbose, EndToEndTraceHelper traceHelper, string durabilityProviderEventSourceName)
        {
            this.logger = logger;
            this.disableVerbose = !enableVerbose; // We track the opposite value ro simplify logic later
            this.traceHelper = traceHelper;
            this.durabilityProviderEventSourceName = durabilityProviderEventSourceName;
        }

        /// <summary>
        /// Gets called for every EventSource in the process, this method allows us to determine
        /// if the listener will subscribe to a particular EventSource provider.
        /// We only listen to DurableTask and DurableTask-Extension EventSource providers.
        /// </summary>
        /// <param name="eventSource">An instance of EventSource.</param>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // "7DA4779A-152E-44A2-A6F2-F80D991A5BEE" is the old DurableTask-Core event source,
            // so we provide extra logic to ignore it.
            if ((eventSource.Name == "DurableTask-Core"
                  && eventSource.Guid != new Guid("7DA4779A-152E-44A2-A6F2-F80D991A5BEE")) ||
                eventSource.Name == "WebJobs-Extensions-DurableTask" ||
                eventSource.Name == this.durabilityProviderEventSourceName)
            {
                this.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
            }
        }

        /// <summary>
        /// Gets called after every EventSource event. We capture that event's data and log it
        /// using the appropiate strategy for the current linux host.
        /// </summary>
        /// <param name="eventData">The EventSource event data, for logging.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // When disabling verbose logs, we skip Verbose DurableTask-Core telemetry
            if (!(this.disableVerbose
               && eventData.EventSource.Name == "DurableTask-Core"
               && eventData.Level == EventLevel.Verbose))
            {
                try
                {
                    this.logger.Log(eventData);
                }
                catch (Exception)
                {
                    this.traceHelper.ExtensionWarningEvent(
                        hubName: string.Empty,
                        instanceId: string.Empty,
                        functionName: string.Empty,
                        message: $"The Linux Logger failed to log, logs might be lost.");
                    throw;
                }
            }
        }
    }
}
