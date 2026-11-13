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

        /// <summary>
        /// Gets or sets a value indicating whether to exclude the system key from generated HTTP
        /// management and creation URLs. Defaults to false for compatibility. When true, callers must
        /// authenticate separately, for example using the x-functions-key request header.
        /// </summary>
        /// <remarks>
        /// Built-in <c>CallHttpAsync</c> automatic 202 polling does not forward <c>x-functions-key</c>.
        /// When this option is enabled, use explicit authenticated polling for key-free management URLs instead.
        /// This option does not change request authentication or redact incoming requests or existing logs.
        /// Keys supplied in query strings are still accepted and may be recorded in HTTP logs.
        /// </remarks>
        public bool ExcludeManagementKeyFromUrls { get; set; } = false;
    }
}
