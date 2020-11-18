// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class OutOfProcExceptionHelpers
    {
        private const string ResultLabel = "Result:";
        private const string MessageLabel = "\nException:";
        private const string StackTraceLabel = "\nStack:";

        private const string OutOfProcDataLabel = "\n\n$OutOfProcData$:";

        /* Extracts <friendly-message> from exceptions with messages of the following form
         * ------------------------------------------------------------------------------
         * Result: Failure
         * Message: <friendly-message>\n\n
         * $OutOfProcData$<out-of-proc-data-as-json>
         * Stack:<stack-trace>
         */
        public static bool TryGetExceptionWithFriendlyMessage(Exception ex, out Exception friendlyMessageException)
        {
            if (!TryGetFullOutOfProcMessage(ex, out string outOfProcMessage))
            {
                friendlyMessageException = null;
                return false;
            }

            string friendlyMessage;
            int jsonIndex = outOfProcMessage.IndexOf(OutOfProcDataLabel);
            if (jsonIndex == -1)
            {
                friendlyMessage = outOfProcMessage;
            }
            else
            {
                friendlyMessage = outOfProcMessage.Substring(0, jsonIndex);
            }

            friendlyMessageException = new Exception(friendlyMessage, ex);
            return true;
        }

        /* Extracts <out-of-proc-data-as-json> from exceptions with messages of the following form
         * ------------------------------------------------------------------------------
         * Result: Failure
         * Message: <friendly-message>\n\n
         * $OutOfProcData$<out-of-proc-data-as-json>
         * Stack:<stack-trace>
         */
        public static bool TryExtractOutOfProcStateJson(Exception ex, out string stateJson)
        {
            if (ex == null)
            {
                stateJson = null;
                return false;
            }

            if (!TryGetFullOutOfProcMessage(ex, out string outOfProcMessage))
            {
                stateJson = null;
                return false;
            }

            int jsonIndex = outOfProcMessage.IndexOf(OutOfProcDataLabel);
            if (jsonIndex == -1)
            {
                stateJson = null;
                return false;
            }
            else
            {
                int jsonStart = jsonIndex + OutOfProcDataLabel.Length;
                int jsonLength = outOfProcMessage.Length - jsonStart;
                stateJson = outOfProcMessage.Substring(jsonStart, jsonLength);
                return true;
            }
        }

        private static bool TryGetFullOutOfProcMessage(Exception ex, out string message)
        {
            if (!IsOutOfProcException(ex))
            {
                message = null;
                return false;
            }

            string rpcExceptionMessage = ex.Message;
            int messageStart = rpcExceptionMessage.IndexOf(MessageLabel) + MessageLabel.Length;
            int messageEnd = rpcExceptionMessage.IndexOf(StackTraceLabel);
            message = rpcExceptionMessage.Substring(messageStart, messageEnd - messageStart);
            return true;
        }

        private static bool IsOutOfProcException(Exception ex)
        {
            // This is a best effort approach to detect when using RpcException from Azure Functions.
            // See https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Workers/Rpc/MessageExtensions/RpcException.cs
            return ex.Message != null
                && ex.Message.StartsWith(ResultLabel)
                && ex.Message.Contains(MessageLabel)
                && ex.Message.Contains(StackTraceLabel);
        }
    }
}