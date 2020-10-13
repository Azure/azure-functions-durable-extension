// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// In charge of logging services for our linux App Service offerings: Consumption and Dedicated.
    /// In Consumption, we log to the console and identify our log by a prefix.
    /// In Dedicated, we log asynchronously to a pre-defined logging path.
    /// This class is utilized by <c>EventSourceListener</c> to write logs corresponding to
    /// specific EventSource providers.
    /// </summary>
    internal class LinuxAppServiceLogger
    {
        private const string ConsolePrefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";

        // The ordered list of regex columns in our legacy logging strategy for linux dedicated.
        private readonly string[] orderedRegexCol = new string[]
        {
            "Account",
            "ActiveActivities",
            "ActiveOrchestrators",
            "Age",
            "AppName",
            "ContinuedAsNew",
            "CreatedTimeFrom",
            "CreatedTimeTo",
            "DequeueCount",
            "Details",
            "Duration",
            "ETag",
            "Episode",
            "EventCount",
            "EventName",
            "EventType",
            "Exception",
            "ExceptionMessage",
            "ExecutionId",
            "ExtensionVersion",
            "FromWorkerName",
            "FunctionName",
            "FunctionState",
            "FunctionType",
            "Input",
            "InstanceId",
            "IsCheckpointComplete",
            "IsExtendedSession",
            "IsReplay",
            "LastCheckpointTime",
            "LatencyMs",
            "MessageId",
            "MessagesRead",
            "MessagesSent",
            "MessagesUpdated",
            "NewEventCount",
            "NewEvents",
            "NextVisibleTime",
            "OperationId",
            "OperationName",
            "Output*",
            "PartitionId",
            "PendingOrchestratorMessages",
            "PendingOrchestrators",
            "Reason",
            "RelatedActivityId",
            "RequestCount",
            "RequestId",
            "RequestingExecutionId",
            "RequestingInstance",
            "RequestingInstanceId",
            "Result",
            "RuntimeStatus",
            "SequenceNumber",
            "SizeInBytes",
            "SlotName",
            "StatusCode",
            "StorageRequests",
            "Success",
            "TableEntitiesRead",
            "TableEntitiesWritten",
            "TargetExecutionId",
            "TargetInstanceId",
            "TaskEventId",
            "TaskHub",
            "Token",
            "TotalEventCount",
            "Version",
            "VisibilityTimeoutSeconds",
            "WorkerName",
        };

        // variable below is internal static for testing and other convenient purposes
        // we need to be able to change the logging path for a windows-based CI
        // the logger being internal static is convenient for flushing it
#pragma warning disable SA1401 // Fields should be private
        internal static string LoggingPath = "/var/log/functionsLogs/durableevents.log";
        internal static LinuxAppServiceFileLogger Logger; // The File Logger
#pragma warning restore SA1401 // Fields should be private

        // logging metadata
        private readonly string roleInstance;
        private readonly string tenant;
        private readonly int procID;
        private readonly string stamp;
        private readonly string primaryStamp;

        // if true, we write to console (linux consumption), else to a file (linux dedicated).
        private readonly bool writeToConsole;

        /// <summary>
        /// Create a LinuxAppServiceLogger instance.
        /// </summary>
        /// <param name="writeToConsole">If true, write to console (linux consumption) else to a file (dedicated).</param>
        /// <param name="containerName">The app's container name.</param>
        /// <param name="tenant">The app's tenant.</param>
        /// <param name="stampName">The app's stamp.</param>
        public LinuxAppServiceLogger(
            bool writeToConsole,
            string containerName,
            string tenant,
            string stampName)
        {
            // Initializing fixed logging metadata
            this.writeToConsole = writeToConsole;

            // Since the values below are obtained via a NameResolver, they might be null.
            // Attempting to serialize a null value results in exceptions, or even worse, wrong logs,
            // so we need to be careful.
            if (!string.IsNullOrEmpty(containerName))
            {
                this.roleInstance = "App-" + containerName;
            }

            this.tenant = tenant;

            if (!string.IsNullOrEmpty(stampName))
            {
                this.stamp = stampName;

                // TODO: The logic below does not apply to ASEs. We'll need to revisit this in the near future.
                var finalCharIndex = stampName.Length - 1;
                this.primaryStamp = char.IsLetter(stampName[finalCharIndex]) ? stampName.Remove(finalCharIndex) : stampName;
            }

            using (var process = Process.GetCurrentProcess())
            {
                this.procID = process.Id;
            }

            // Initialize file logger, if in Linux Dedicated
            if (!writeToConsole)
            {
                // int tenMbInBytes = 10000000;
                string fname = Path.GetFileName(LinuxAppServiceLogger.LoggingPath);
                string dir = Path.GetDirectoryName(LinuxAppServiceLogger.LoggingPath);
                Logger = new LinuxAppServiceFileLogger(fname, dir);
            }
        }

        /// <summary>
        /// Given EventSource message data, we generate a JSON-string that we can log.
        /// </summary>
        /// <param name="eventData">An EventSource message, usually generated by an EventListener.</param>
        /// <returns>A JSON-formatted string representing the input.</returns>
        private string GenerateLogStr(EventWrittenEventArgs eventData)
        {
            var values = eventData.Payload;
            var keys = eventData.PayloadNames;

            // We pack them into a JSON
            JObject json = new JObject
            {
                { "ProviderName", eventData.EventSource.Name },
                { "TaskName", eventData.EventName },
                { "EventId", eventData.EventId },
                { "EventTimestamp", DateTime.UtcNow },
                { "Pid", this.procID },
                { "Tid", Thread.CurrentThread.ManagedThreadId },
                { "Level", (int)eventData.Level },
            };

            if (!string.IsNullOrEmpty(this.stamp) && !string.IsNullOrEmpty(this.primaryStamp))
            {
                json.Add("EventStampName", this.stamp);
                json.Add("EventPrimaryStampName", this.primaryStamp);
            }

            if (!(this.roleInstance is null))
            {
                json.Add("RoleInstance", this.roleInstance);
            }

            if (!(this.tenant is null))
            {
                json.Add("Tenant", this.tenant);
            }

            // Add payload elements
            for (int i = 0; i < values.Count; i++)
            {
                json.Add(keys[i], JToken.FromObject(values[i]));
            }

            // These are for ease of use in our linux-dedicated regex-compensation strategy
            string activityId = "";
            string relatedActivityId = "";

            // Add ActivityId and RelatedActivityId, if non-null
            if (!eventData.ActivityId.Equals(Guid.Empty))
            {
                json.Add("ActivityId", eventData.ActivityId);
                activityId = eventData.ActivityId.ToString();
            }

            if (!eventData.RelatedActivityId.Equals(Guid.Empty))
            {
                json.Add("RelatedActivityId", eventData.RelatedActivityId);
                relatedActivityId = eventData.RelatedActivityId.ToString();
            }

            string logString;
            if (!this.writeToConsole)
            {
                // This path supports the legacy regex-based parser in Linux Dedicated.
                // In the future, we'll be able to remove this and use JSON logging only
                List<string> regexValues = new List<string>();

                foreach (string column in this.orderedRegexCol)
                {
                    string val = "";
                    if (json.TryGetValue(column, out JToken valJToken))
                    {
                        // We escape a few special characters to avoid parsing problems:
                        // (1) Escaping newline-like characters such as \n and \r to keep logs being 1 line
                        // (2) Escaping double-quotes (") to be single-quotes (') because some fields in our regex string are
                        //     deliniated by double-quotes. Note, this was a convention copied from the Functions Host regex
                        //     which also uses double-quotes to capture columns that may contain commas inside them.
                        // (3) Escaping commas (",") for ";;" because commas separate our columns and so they have the potential to
                        //     disrupt parsing.
                        // Note: In retrospective, perhaps the regex-string could have been designed to avoid these awkward
                        // parsing problems, but it wasn't because (1) it followed conventions from other regex-strings in our system
                        // and because we asssumed we'd have a JSON-based logger that would have avoided these problems.
                        val = (string)valJToken;
                        val = val.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\"", "'").Replace(",", ";;");
                    }

                    if (column == "Details" || column == "ExceptionMessage")
                    {
                        // Since Details and Exceptions may include commas, our regex string
                        // expects this field to be wrapped in double-quotes to avoid capturing an "inner comma",
                        // a convention adopted by the Functions Host regex string.
                        val = '"' + val + '"';
                    }

                    regexValues.Add(val);
                }

                logString = string.Join(",", regexValues);

                // To compensate for the missing columns in our regex, we write extra columns to WorkerName while separated
                // with a special delineator: ";;DURABLEFUNCTIONS;;"
                string delineator = ";;DURABLEFUNCTIONS;;";
                string[] extraCols =
                {
                    (string)json["TaskName"],
                    (string)json["EventId"],
                    (string)json["ProviderName"],
                    (string)json["Level"],
                    (string)json["Pid"],
                    (string)json["Tid"],
                    (string)json["EventTimestamp"],
                    activityId,
                    relatedActivityId,
                };

                logString += delineator + string.Join(delineator, extraCols);
            }
            else
            {
                // Generate string-representation of JSON.
                // Newtonsoft should take care of removing newlines for us.
                // It is also important to specify no formatting to avoid
                // pretty printing.
                logString = json.ToString(Newtonsoft.Json.Formatting.None);
            }

            return logString;
        }

        /// <summary>
        /// Log EventSource message data in Linux AppService.
        /// </summary>
        /// <param name="eventData">An EventSource message, usually generated by an EventListener.</param>
        public void Log(EventWrittenEventArgs eventData)
        {
            // Generate JSON string to log based on the EventSource message
            string jsonString = this.GenerateLogStr(eventData);

            // We write to console in Linux Consumption
            if (this.writeToConsole)
            {
                // We're ignoring exceptions in the unobserved Task
                string consoleLine = ConsolePrefix + " " + jsonString;
                _ = Console.Out.WriteLineAsync(consoleLine);
            }
            else
            {
                // We write to a file in Linux Dedicated
                // Our file logger already handles file rolling (archiving) and deletion of old logs
                Logger.Log(jsonString);
            }
        }
    }
}