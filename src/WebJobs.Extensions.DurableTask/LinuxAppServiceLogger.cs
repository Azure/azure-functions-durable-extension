﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// In charge of logging services for our linux App Service offerings: Consumption and Dedicated.
    /// In Consumption, we log to the console and identify our log by a prefix.
    /// In Dedicated, we log to a pre-defined logging path.
    /// This class is utilized by <c>EventSourceListener</c> to write logs corresponding to
    /// specific EventSource providers.
    /// </summary>
    internal class LinuxAppServiceLogger
    {
        private const string ConsolePrefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
        private const string LoggingPath = "/var/log/functionsLogs/durableevents.log";
        private const int MaxArchives = 5;
        private const int MaxLogfileSizeInMb = 10;
        private const int BytesToMb = 1024 * 1024;

        // logging metadata
        private readonly JToken roleInstance;
        private readonly JToken tenant;
        private readonly JToken sourceMoniker;

        // if true, we write to console (linux consumption), else to a file (linux dedicated).
        private readonly bool writeToConsole;

        // the paths to all allowed archived log files.
        private readonly string[] archivedPaths = new string[MaxArchives];

        // the current number of archived log files
        private int countArchives = 0;

        /// <summary>
        /// Create a LinuxAppServiceLogger instance.
        /// </summary>
        /// <param name="writeToConsole">If true, write to console (linux consumption) else to a file (dedicated).</param>
        /// <param name="containerName">The app's container name.</param>
        /// <param name="tenant">The app's tenant.</param>
        /// <param name="stampName">The app's stamp.</param>
        public LinuxAppServiceLogger(bool writeToConsole, string containerName, string tenant, string stampName)
        {
            // If writeToConsole is False, we write to a file
            for (int count = 1; count <= MaxArchives; count++)
            {
                string archivedPath = LoggingPath + count;
                this.archivedPaths[count - 1] = archivedPath;
            }

            // initializing fixed logging metadata
            this.writeToConsole = writeToConsole;
            this.roleInstance = JToken.FromObject("App-" + containerName);
            this.tenant = JToken.FromObject(tenant);
            this.sourceMoniker = JToken.FromObject(
                string.IsNullOrEmpty(stampName) ? string.Empty : "L" + stampName.Replace("-", "").ToUpperInvariant());
        }

        /// <summary>
        /// Given EventSource message data, we generate a JSON-string that we can log.
        /// </summary>
        /// <param name="eventData">An EventSource message, usually generated by an EventListener.</param>
        /// <returns>A JSON-formatted string representing the input.</returns>
        private string GenerateJsonStr(EventWrittenEventArgs eventData)
        {
            var values = eventData.Payload;
            var keys = eventData.PayloadNames;

            // We pack them into a JSON
            JObject json = new JObject
            {
                { "EventId", eventData.EventId },
                { "TimeStamp", DateTime.UtcNow },
                { "RoleInstance", this.roleInstance },
                { "Tenant", this.tenant },
                { "SourceMoniker",  this.sourceMoniker },
            };
            for (int i = 0; i < values.Count; i++)
            {
                json.Add(keys[i], JToken.FromObject(values[i]));
            }

            // Generate string-representation of JSON
            string jsonString = json.ToString(Newtonsoft.Json.Formatting.None);
            return jsonString;
        }

        /// <summary>
        /// Log EventSource message data in Linux AppService.
        /// </summary>
        /// <param name="eventData">An EventSource message, usually generated by an EventListener.</param>
        public void Log(EventWrittenEventArgs eventData)
        {
            // Generate JSON string to log based on the EventSource message
            string jsonString = this.GenerateJsonStr(eventData);

            // We write to console in Linux Consumption
            if (this.writeToConsole)
            {
                // We're ignoring exceptions in the unobserved Task
                string consoleLine = ConsolePrefix + " " + jsonString;
                Task unused = Console.Out.WriteLineAsync(consoleLine);
            }
            else
            {
                // If the log file gets too big, we archive it.
                FileInfo logFileInfo = new FileInfo(LoggingPath);
                if (logFileInfo.Length / BytesToMb >= MaxLogfileSizeInMb)
                {
                    string archivedPath = this.archivedPaths[this.countArchives];
                    File.Move(LoggingPath, archivedPath);
                    this.countArchives++;
                }

                // If we have too many archived log files, we delete them
                if (this.countArchives > MaxArchives)
                {
                    foreach (string archivePath in this.archivedPaths)
                    {
                        File.Delete(archivePath);
                    }

                    this.countArchives = 0;
                }

                // We write to a file in Linux Dedicated
                var writer = new StreamWriter(LoggingPath, append: true);

                // We're ignoring exceptions in the unobserved Task
                Task unused = writer.WriteLineAsync(jsonString).ContinueWith(_ => writer.Dispose());
            }
        }
    }
}
