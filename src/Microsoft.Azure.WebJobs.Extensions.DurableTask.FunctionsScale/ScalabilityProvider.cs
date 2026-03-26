// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// The backend storage scalability provider for Durable Functions.
    /// </summary>
    public class ScalabilityProvider
    {
        internal const string NoConnectionDetails = "default";

        private readonly string name;
        private readonly string connectionName;
        private int maxConcurrentTaskOrchestrationWorkItems;
        private int maxConcurrentTaskActivityWorkItems;
        private int maxConcurrentTaskEntityWorkItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScalabilityProvider"/> class.
        /// </summary>
        /// <param name="storageProviderName">The name of the storage backend providing the durability.</param>
        /// <param name="connectionName">The name of the app setting that stores connection details for the storage provider.</param>
        public ScalabilityProvider(string storageProviderName, string connectionName)
        {
            this.name = storageProviderName ?? throw new ArgumentNullException(nameof(storageProviderName));
            this.connectionName = connectionName ?? throw new ArgumentNullException(nameof(connectionName));
            this.maxConcurrentTaskOrchestrationWorkItems = 10; // Default value
            this.maxConcurrentTaskActivityWorkItems = 10; // Default value
            this.maxConcurrentTaskEntityWorkItems = 10; // Default value
        }

        /// <summary>
        /// Gets the name of the environment variable that contains connection details for how to connect to storage providers.
        /// Corresponds to the <see cref="DurableClientAttribute.ConnectionName"/> for binding data.
        /// </summary>
        public virtual string ConnectionName => this.connectionName;

        /// <summary>
        /// Gets or sets the maximum number of concurrent orchestration work items.
        /// </summary>
        public virtual int MaxConcurrentTaskOrchestrationWorkItems
        {
            get => this.maxConcurrentTaskOrchestrationWorkItems;
            set => this.maxConcurrentTaskOrchestrationWorkItems = value;
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent activity work items.
        /// </summary>
        public virtual int MaxConcurrentTaskActivityWorkItems
        {
            get => this.maxConcurrentTaskActivityWorkItems;
            set => this.maxConcurrentTaskActivityWorkItems = value;
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent entity work items.
        /// </summary>
        public virtual int MaxConcurrentTaskEntityWorkItems
        {
            get => this.maxConcurrentTaskEntityWorkItems;
            set => this.maxConcurrentTaskEntityWorkItems = value;
        }

        /// <summary>
        /// Tries to obtain a scale monitor for autoscaling.
        /// </summary>
        /// <param name="functionId">Function id.</param>
        /// <param name="functionName">Function name.</param>
        /// <param name="hubName">Task hub name.</param>
        /// <param name="connectionName">The name of the storage-specific connection settings.</param>
        /// <param name="scaleMonitor">The scale monitor.</param>
        /// <returns>True if autoscaling is supported, false otherwise.</returns>
        public virtual bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out IScaleMonitor scaleMonitor)
        {
            scaleMonitor = null;
            return false;
        }

        /// <summary>
        /// Tries to obtain a scaler for target based scaling.
        /// </summary>
        /// <param name="functionId">Function id.</param>
        /// <param name="functionName">Function name.</param>
        /// <param name="hubName">Task hub name.</param>
        /// <param name="connectionName">The name of the storage-specific connection settings.</param>
        /// <param name="targetScaler">The target-based scaler.</param>
        /// <returns>True if target-based scaling is supported, false otherwise.</returns>
        public virtual bool TryGetTargetScaler(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out ITargetScaler targetScaler)
        {
            targetScaler = null;
            return false;
        }

        /// <summary>
        ///  Returns true if the stored connection string, ConnectionName, matches the input ScalabilityProvider ConnectionName.
        /// </summary>
        /// <param name="durabilityProvider">The ScalabilityProvider used to check for matching connection string names.</param>
        /// <returns>A boolean indicating whether the connection names match.</returns>
        internal virtual bool ConnectionNameMatches(ScalabilityProvider durabilityProvider)
        {
            return this.ConnectionName.Equals(durabilityProvider.ConnectionName);
        }
    }
}
