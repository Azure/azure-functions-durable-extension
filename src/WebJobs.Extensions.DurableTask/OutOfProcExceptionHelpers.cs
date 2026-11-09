// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            if (!TryGetFailureStateJson(outOfProcMessage, out _, out int jsonIndex))
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
            if (!TryGetFullOutOfProcMessage(ex, out string outOfProcMessage))
            {
                stateJson = null;
                return false;
            }

            return TryGetFailureStateJson(outOfProcMessage, out stateJson, out _);
        }

        private static bool TryGetFailureStateJson(string message, out string stateJson, out int jsonIndex)
        {
            stateJson = null;

            // Workers append their own envelope after the original error. Copied/nested errors
            // can contain earlier markers; JSON strings in the envelope escape literal newlines.
            jsonIndex = message.LastIndexOf(OutOfProcDataLabel, StringComparison.Ordinal);
            if (jsonIndex < 0)
            {
                return false;
            }

            string candidate = message.Substring(jsonIndex + OutOfProcDataLabel.Length);
            try
            {
                JObject state = JObject.Parse(candidate);
                JToken isDone = state.GetValue("isDone", StringComparison.OrdinalIgnoreCase);
                JToken error = state.GetValue("error", StringComparison.OrdinalIgnoreCase);
                JToken actions = state.GetValue("actions", StringComparison.OrdinalIgnoreCase);

                // Only recover failure envelopes. Arbitrary JSON (including a completion result)
                // in an exception must not replace the original failure or schedule its actions.
                if (state.Properties().GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)
                    || isDone?.Type != JTokenType.Boolean
                    || isDone.Value<bool>()
                    || error?.Type != JTokenType.String
                    || string.IsNullOrEmpty(error.Value<string>())
                    || !(actions is JArray actionSets)
                    || actionSets.Any(actionSet => !(actionSet is JArray batch) || batch.Any(action => !(action is JObject))))
                {
                    return false;
                }

                stateJson = candidate;
                return true;
            }
            catch (JsonException)
            {
                // The caller reports the original worker exception when no valid state is present.
                return false;
            }
        }

        private static bool TryGetFullOutOfProcMessage(Exception ex, out string message)
        {
            if (!IsOutOfProcException(ex))
            {
                message = null;
                return false;
            }

            // Avoid parsing labels that may also occur in error text or in a JavaScript stack
            // that repeats that text. The Script host is deliberately not a package dependency.
            for (Type type = ex.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == "Microsoft.Azure.WebJobs.Script.Workers.Rpc.RpcException"
                    && type.GetProperty("RemoteMessage")?.GetValue(ex) is string remoteMessage)
                {
                    message = remoteMessage;
                    return true;
                }
            }

            string rpcExceptionMessage = ex.Message;
            int messageStart = rpcExceptionMessage.IndexOf(MessageLabel, StringComparison.Ordinal) + MessageLabel.Length;

            // The original error can contain a complete RPC exception, including its Stack label.
            int messageEnd = rpcExceptionMessage.LastIndexOf(StackTraceLabel, StringComparison.Ordinal);
            if (messageEnd < messageStart)
            {
                message = null;
                return false;
            }

            message = rpcExceptionMessage.Substring(messageStart, messageEnd - messageStart);
            return true;
        }

        private static bool IsOutOfProcException(Exception ex)
        {
            // This is a best effort approach to detect when using RpcException from Azure Functions.
            // See https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Workers/Rpc/MessageExtensions/RpcException.cs
            return ex?.Message != null
                && ex.Message.StartsWith(ResultLabel, StringComparison.Ordinal)
                && ex.Message.Contains(MessageLabel)
                && ex.Message.Contains(StackTraceLabel);
        }
    }
}