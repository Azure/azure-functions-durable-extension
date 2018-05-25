// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Data structure containing status, terminate and send external event HTTP endpoints
    /// </summary>
    public class CheckStatus
    {
        /// <summary>
        /// Check Status constructor
        /// </summary>
        /// <param name="requestUri">Request Uri</param>
        /// <param name="notificationUri">Notification Uri</param>
        /// <param name="instanceId">Orchestration instance id</param>
        /// <param name="taskHubName">Task hub name</param>
        /// <param name="connectionName">Property name for Azure Storage Connection String</param>
        internal CheckStatus(
            Uri requestUri,
            Uri notificationUri,
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            if (notificationUri == null)
            {
                throw new InvalidOperationException("Webhooks are not configured");
            }

            Uri baseUri = requestUri ?? notificationUri;

            // e.g. http://{host}/admin/extensions/DurableTaskExtension?code={systemKey}
            string hostUrl = baseUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            string instancePrefix = baseUrl + CheckStatusConstants.InstancesControllerSegment + WebUtility.UrlEncode(instanceId);

            string taskHub = WebUtility.UrlEncode(taskHubName);
            string connection = WebUtility.UrlEncode(connectionName ?? ConnectionStringNames.Storage);

            string querySuffix = $"{CheckStatusConstants.TaskHubParameter}={taskHub}&{CheckStatusConstants.ConnectionParameter}={connection}";
            if (!string.IsNullOrEmpty(notificationUri.Query))
            {
                // This is expected to include the auto-generated system key for this extension.
                querySuffix += "&" + notificationUri.Query.TrimStart('?');
            }

            this.Id = instanceId;
            this.StatusQueryGetUri = instancePrefix + "?" + querySuffix;
            this.SendEventPostUri = instancePrefix + "/" + CheckStatusConstants.RaiseEventOperation + "/{eventName}?" + querySuffix;
            this.TerminatePostUri = instancePrefix + "/" + CheckStatusConstants.TerminateOperation + "?reason={text}&" + querySuffix;
        }

        /// <summary>
        /// Instance ID
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Status endpoint
        /// </summary>
        [JsonProperty("statusQueryGetUri")]
        public string StatusQueryGetUri { get; set; }

        /// <summary>
        /// Send external event endpoint
        /// </summary>
        [JsonProperty("sendEventPostUri")]
        public string SendEventPostUri { get; set; }

        /// <summary>
        /// Terminate endpoint
        /// </summary>
        [JsonProperty("terminatePostUri")]
        public string TerminatePostUri { get; set; }
    }
}
