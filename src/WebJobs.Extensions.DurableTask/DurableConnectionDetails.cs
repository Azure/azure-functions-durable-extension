// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Storage account connection details to perform operations on orchestrator and entity functions across function apps.
    /// </summary>
    public class DurableConnectionDetails
    {
        /// <summary>
        /// The task hub name associated with the target function.
        /// </summary>
        public string TaskHub { get; set; }

        /// <summary>
        /// The connection name associated with the target function.
        /// </summary>
        public string ConnectionName { get; set; }
    }
}
