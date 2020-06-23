// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Produces Telemetry for the Linux App Service offerings.
    /// </summary>
    internal class LinuxEventWriter
    {
        private static readonly string ConsumptionPrefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";

        private static LinuxDedicatedLogger logger;

        // TODO: change these vars for a state enum
        private static bool inLinuxConsumption = false;
        private static bool inLinuxDedicated = false;

        /// <summary>
        /// Indicates that logs should be written for Linux Consumption.
        /// Should be called before the first log is written, when the
        /// application is initialized.
        /// </summary>
        public static void FlagLinuxConsumption()
        {
            // TODO: Raise exception if linux dedicated had already been flagged
            inLinuxConsumption = true;
            inLinuxDedicated = !inLinuxConsumption;
        }

        /// <summary>
        /// Indicates that logs should be written for Linux Dedicated.
        /// Should be called before the first log is written, when the
        /// application is initialized.
        /// </summary>
        /// <param name="dirPath">Path to directory where log file will reside.</param>
        /// <param name="fileName">Name of log file.</param>
        public static void FlagLinuxDedicated(string dirPath, string fileName)
        {
            // TODO: Raise exception if linux consumption had already been flagged
            logger = new LinuxDedicatedLogger(fileName, dirPath, new FileSystem());
            inLinuxDedicated = true;
            inLinuxConsumption = !inLinuxDedicated;
        }

        /// <summary>
        /// Returns True if running on Linux Consumption or Dedicated. False otherwise.
        /// </summary>
        /// <returns>True if running on Linux Consumption or Dedicated. False otherwise.</returns>
        public static bool IsEnabled()
        {
            // TODO: May want to cache this result as it should not change
            return inLinuxDedicated || inLinuxConsumption;
        }

        /// <summary>
        /// Writes a line asynchronously to a file.
        /// We use this instead of an ILogger because
        /// we expect to write often and thus need asynchronous
        /// writing, which ILogger explicitely designs against.
        /// </summary>
        /// <param name="line">Line to write to log-file.</param>
        private static void WriteToFile(string line)
        {
            LinuxEventWriter.FlagLinuxDedicated("/var/log/functionsLogs", "durableevents");
            logger.Log(line);
        }

        /// <summary>
        /// Writes a line asynchronously to the console.
        /// It adds a prefix to the line, so it can get picked up
        /// by the regex monitoring agent.
        /// </summary>
        /// <param name="line">Line to write to console.</param>
        private static void ConsoleWriter(string line)
        {
            line = ConsumptionPrefix + " " + line;
            Console.WriteLine(line);
        }

        /// <summary>
        /// Writes a line asynchronously to either the console
        /// (linux consumption) or a log file (linux dedicated).
        /// </summary>
        /// <param name="line">Line to write.</param>
        public static void Write(string line)
        {
            // TODO: may want to explicitely check for linux consumption
            // and to raise an error in a catch-all case
            if (inLinuxDedicated)
            {
                WriteToFile(line);
            }
            else
            {
                ConsoleWriter(line);
            }
        }
    }
}
