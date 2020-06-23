// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A string-serializable collection of data representing a durable event
    /// to be logged in Linux.
    /// </summary>
    public class DurableFunctionsLinuxLog
    {
        /// <summary>
        /// Turns Log into a string.
        /// </summary>
        /// <returns>String-representation of the log.</returns>
        public static string AsString(Dictionary<string, string> record)
        {
            string log = "";
            List<string> orderedColumns = new List<string>
            {
                "Account", "ActiveActivities", "ActiveOrchestrators",
                "Age", "AppName", "ContinuedAsNew", "CreatedTimeFrom",
                "CreatedTimeTo", "DequeueCount", "Details", "Duration",
                "ETag", "Episode", "EventCount", "EventName", "EventType",
                "Exception", "ExceptionMessage", "ExecutionId", "ExtensionVersion",
                "FromWorkerName", "FunctionName", "FunctionState", "FunctionType",
                "Input", "InstanceId", "IsCheckpointComplete", "IsExtendedSession",
                "IsReplay", "LastCheckpointTime", "LatencyMs", "MessageId", "MessagesRead",
                "MessagesSent", "MessagesUpdated", "NewEventCount", "NewEvents", "NextVisibleTime",
                "OperationId", "OperationName", "Output", "PartitionId", "PendingOrchestratorMessages",
                "PendingOrchestrators", "Reason", "RelatedActivityId", "RequestCount", "RequestId",
                "RequestingExecutionId", "RequestingInstance", "RequestingInstanceId", "Result",
                "RuntimeStatus", "SequenceNumber", "SizeInBytes", "SlotName", "StatusCode",
                "StorageRequests", "Success", "TableEntitiesRead", "TableEntitiesWritten",
                "TargetExecutionId", "TargetInstanceId", "TaskEventId", "TaskHub", "Token",
                "TotalEventCount", "Version", "VisibilityTimeoutSeconds", "WorkerName",
            };

            // to represent the value of a column
            string value;

            // we write each column in order
            foreach (string column in orderedColumns)
            {
                value = "";

                // Details and ExceptionMessage are double-quote-wrapped
                // messages, so their default value is '""'
                if (column == "Details" || column == "ExceptionMessage")
                {
                    value = "\"\"";
                }

                if (record.ContainsKey(column))
                {
                    value = record[column];
                }

                log += value + ",";
            }

            int logLength = log.Length;
            log = log.Remove(logLength - 1);

            return log;
        }
    }
}
