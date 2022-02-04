// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Options used to bind a function parameter to a <see cref="IDurableClient"/>, <see cref="IDurableEntityClient"/>, or <see cref="IDurableOrchestrationClient"/> instance.
    /// </summary>
    public class DurableClientOptions
    {
        /// <summary>
        /// Optional. Gets or sets the setting name for the app setting containing connection details used by this binding to connect
        /// to instances of the storage provider other than the default one this application communicates with.
        /// </summary>
        /// <value>The name of an app setting containing connection details.</value>
        /// <remarks>
        /// For Azure Storage the default behavior is to use the value of <see cref="AzureStorageOptions.ConnectionName"/>.
        /// If no value exists there, then the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        public string ConnectionName { get; set; }

        /// <summary>
        ///  Gets or sets the name of the task hub in which the orchestration data lives.
        /// </summary>
        /// <value>The task hub used by this binding.</value>
        /// <remarks>
        /// The default behavior is to use the task hub name specified in <see cref="DurableTaskOptions.HubName"/>.
        /// If no value exists there, then a default value will be used.
        /// </remarks>
        public string TaskHub { get; set; }

        /// <summary>
        ///     Indicate if the client is External from the azure function where orchestrator functions are hosted.
        /// </summary>
        /// <remarks>
        ///     Default is true.
        /// </remarks>
        public bool IsExternalClient { get; set; } = true;
    }
}