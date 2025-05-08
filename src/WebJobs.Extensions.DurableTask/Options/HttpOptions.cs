// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Used for Durable HTTP functionality.
    /// </summary>
    public class HttpOptions
    {
        /// <summary>
        /// Reserved name to know when a TaskActivity should be an HTTP activity.
        /// </summary>
        internal const string HttpTaskActivityReservedName = "BuiltIn::HttpActivity";

        /// <summary>
        /// Gets or sets the default number of milliseconds between async HTTP status poll requests.
        /// </summary>
        public int DefaultAsyncRequestSleepTimeMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Gets or sets a value indicating whether to use forwarded headers for URL construction.
        /// </summary>
        public bool UseForwardedHost { get; set; } = false;
    }
}
