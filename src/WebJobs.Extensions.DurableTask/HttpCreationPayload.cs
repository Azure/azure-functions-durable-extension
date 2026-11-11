// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Data structure containing orchestration instance creation HTTP endpoints.
    /// </summary>
    internal class HttpCreationPayload
    {
        /// <summary>
        /// Gets the headers callers must send when using the creation URLs.
        /// </summary>
        [JsonProperty("requiredHeaders")]
        internal IReadOnlyDictionary<string, string> RequiredHeaders { get; set; }

        /// <summary>
        /// Gets the HTTP POST orchestration instance creation endpoint URL.
        /// </summary>
        /// <value>
        /// The HTTP URL for creating a new orchestration instance.
        /// </value>
        [JsonProperty("createNewInstancePostUri")]
        internal string CreateNewInstancePostUri { get; set; }

        /// <summary>
        /// Gets the HTTP POST orchestration instance create-and-wait endpoint URL.
        /// </summary>
        /// <value>
        /// The HTTP URL for creating a new orchestration instance and waiting on its completion.
        /// </value>
        [JsonProperty("createAndWaitOnNewInstancePostUri")]
        internal string CreateAndWaitOnNewInstancePostUri { get; set; }
    }
}
