// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Data structure containing status, terminate and send external event HTTP endpoints.
    /// </summary>
    public class HttpManagementPayload
    {
        /// <summary>
        /// Gets the ID of the orchestration instance.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; internal set; }

        /// <summary>
        /// Gets the HTTP GET status query endpoint URL.
        /// </summary>
        [JsonProperty("statusQueryGetUri")]
        public string StatusQueryGetUri { get; internal set; }

        /// <summary>
        /// Gets the HTTP POST external event sending endpoint URL.
        /// </summary>
        [JsonProperty("sendEventPostUri")]
        public string SendEventPostUri { get; internal set; }

        /// <summary>
        /// Gets the HTTP POST instance termination endpoint.
        /// </summary>
        [JsonProperty("terminatePostUri")]
        public string TerminatePostUri { get; internal set; }
    }
}
