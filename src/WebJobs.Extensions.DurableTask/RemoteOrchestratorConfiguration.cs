// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration settings for RemoteOrchestratorContext in out-of-process mode, transmitted via gRPC.
    /// </summary>
    public class RemoteOrchestratorConfiguration
    {
        /// <summary>
        /// Gets or sets the default number of milliseconds between async HTTP status poll requests.
        /// </summary>
        public int HttpDefaultAsyncRequestSleepTimeMilliseconds { get; set; } = 30000;
    }
}
