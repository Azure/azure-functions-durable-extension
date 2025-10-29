// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Entities;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using Microsoft.Azure.WebJobs.Host.Scale;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// The backend storage provider that provides the actual durability of Durable Functions.
    /// This is functionally a superset of <see cref="IOrchestrationService"/> and <see cref="IOrchestrationServiceClient"/>.
    /// If the storage provider does not any of the Durable Functions specific operations, they can use this class
    /// directly with the expectation that only those interfaces will be implemented. All of the Durable Functions specific
    /// methods/operations are virtual and can be overwritten by creating a subclass.
    /// </summary>
    public class DurabilityProvider
    {
        internal const string NoConnectionDetails = "default";

        private static readonly JObject EmptyConfig = new JObject();

        private readonly string name;
        private readonly IOrchestrationService innerService;
        private readonly IOrchestrationServiceClient innerServiceClient;
        private readonly IEntityOrchestrationService entityOrchestrationService;
        private readonly string connectionName;

        /// <summary>
        /// Creates the default <see cref="DurabilityProvider"/>.
        /// </summary>
        /// <param name="storageProviderName">The name of the storage backend providing the durability.</param>
        /// <param name="service">The internal <see cref="IOrchestrationService"/> that provides functionality
        /// for this classes implementions of <see cref="IOrchestrationService"/>.</param>
        /// <param name="serviceClient">The internal <see cref="IOrchestrationServiceClient"/> that provides functionality
        /// for this classes implementions of <see cref="IOrchestrationServiceClient"/>.</param>
        /// <param name="connectionName">The name of the app setting that stores connection details for the storage provider.</param>
        public DurabilityProvider(string storageProviderName, IOrchestrationService service, IOrchestrationServiceClient serviceClient, string connectionName)
        {
            this.name = storageProviderName ?? throw new ArgumentNullException(nameof(storageProviderName));
            this.innerService = service ?? throw new ArgumentNullException(nameof(service));
            this.entityOrchestrationService = service as IEntityOrchestrationService;
            this.connectionName = connectionName ?? throw new ArgumentNullException(connectionName);
        }

        /// <summary>
        /// The name of the environment variable that contains connection details for how to connect to storage providers.
        /// Corresponds to the <see cref="DurableClientAttribute.ConnectionName"/> for binding data.
        /// </summary>
        public virtual string ConnectionName => this.connectionName;

        /// <summary>
        /// Specifies whether the durability provider supports Durable Entities.
        /// </summary>
        public virtual bool SupportsEntities => this.entityOrchestrationService?.EntityBackendProperties != null;

        /// <summary>
        /// JSON representation of configuration to emit in telemetry.
        /// </summary>
        public virtual JObject ConfigurationJson => EmptyConfig;

        /// <summary>
        /// Event source name (e.g. DurableTask-AzureStorage).
        /// </summary>
        public virtual string EventSourceName { get; set; }

        /// <inheritdoc/>
        public int MaxConcurrentTaskOrchestrationWorkItems => this.innerService.MaxConcurrentTaskOrchestrationWorkItems;

        /// <inheritdoc/>
        public int MaxConcurrentTaskActivityWorkItems => this.innerService.MaxConcurrentTaskActivityWorkItems;

        /// <summary>
        ///  Returns true if the stored connection string, ConnectionName, matches the input DurabilityProvider ConnectionName.
        /// </summary>
        /// <param name="durabilityProvider">The DurabilityProvider used to check for matching connection string names.</param>
        /// <returns>A boolean indicating whether the connection names match.</returns>
        internal virtual bool ConnectionNameMatches(DurabilityProvider durabilityProvider)
        {
            return this.ConnectionName.Equals(durabilityProvider.ConnectionName);
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
            string connectionName,
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
            string connectionName,
            out ITargetScaler targetScaler)
        {
            targetScaler = null;
            return false;
        }
    }
}
