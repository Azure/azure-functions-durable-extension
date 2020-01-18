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

        public static bool IsRpcException(Exception ex)
        {
            // This is a best effort approach to detect when using RpcException
            return ex.Message != null
                && ex.Message.StartsWith(ResultLabel)
                && ex.Message.Contains(MessageLabel)
                && ex.Message.Contains(StackTraceLabel);
        }

        public static Exception GetExceptionWithFriendlyMessage(Exception ex)
        {
            if (!IsRpcException(ex))
            {
                return ex;
            }

            string friendlyMessage = ExtractFriendlyMessage(ex);
            return new Exception(friendlyMessage, ex);
        }

        public static string ExtractOutOfProcStateJson(Exception ex)
        {
            if (!IsRpcException(ex))
            {
                throw new ArgumentException($"Exception with message {ex.Message} does not match the RpcException schema");
            }

            string outOfProcMessage = GetFullOutOfProcMessage(ex);
            int jsonIndex = outOfProcMessage.IndexOf(OutOfProcDataLabel);
            if (jsonIndex == -1)
            {
                return string.Empty;
            }
            else
            {
                int jsonStart = jsonIndex + OutOfProcDataLabel.Length;
                int jsonLength = outOfProcMessage.Length - jsonStart;
                return outOfProcMessage.Substring(jsonStart, jsonLength);
            }
        }

        private static string GetFullOutOfProcMessage(Exception ex)
        {
            string rpcExceptionMessage = ex.Message;

            int messageStart = rpcExceptionMessage.IndexOf(MessageLabel) + MessageLabel.Length;
            int messageEnd = rpcExceptionMessage.IndexOf(StackTraceLabel);
            return rpcExceptionMessage.Substring(messageStart, messageEnd - messageStart);
        }

        private static string ExtractFriendlyMessage(Exception ex)
        {
            string outOfProcMessage = GetFullOutOfProcMessage(ex);
            int jsonIndex = outOfProcMessage.IndexOf(OutOfProcDataLabel);
            if (jsonIndex == -1)
            {
                return outOfProcMessage;
            }
            else
            {
                return outOfProcMessage.Substring(0, jsonIndex);
            }
        }


    }
}
