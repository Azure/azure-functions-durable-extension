// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage
{
    /// <summary>
    /// Azure Storage backend implementation of the scalability provider for Durable Functions scaling decisions.
    /// </summary>
    public class AzureStorageScalabilityProvider : ScalabilityProvider
    {
        private readonly StorageAccountClientProvider storageAccountClientProvider;
        private readonly string connectionName;
        private readonly ILogger logger;

        private readonly object initLock = new object();

        private DurableTaskMetricsProvider singletonDurableTaskMetricsProvider;

        public AzureStorageScalabilityProvider(
            StorageAccountClientProvider storageAccountClientProvider,
            string connectionName,
            ILogger logger)
            : base("AzureStorage", connectionName)
        {
            this.storageAccountClientProvider = storageAccountClientProvider ?? throw new ArgumentNullException(nameof(storageAccountClientProvider));
            this.connectionName = connectionName;
            this.logger = logger;
        }

        /// <summary>
        /// The app setting containing the Azure Storage connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        internal DurableTaskMetricsProvider GetMetricsProvider(
            string hubName,
            StorageAccountClientProvider storageAccountClientProvider,
            ILogger logger)
        {
            return new DurableTaskMetricsProvider(hubName, logger, performanceMonitor: null, storageAccountClientProvider);
        }

        /// <inheritdoc/>
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out IScaleMonitor scaleMonitor)
        {
            lock (this.initLock)
            {
                if (this.singletonDurableTaskMetricsProvider == null)
                {
                    this.singletonDurableTaskMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.storageAccountClientProvider,
                        this.logger);
                }

                scaleMonitor = new DurableTaskScaleMonitor(functionId, hubName, this.logger, this.singletonDurableTaskMetricsProvider);
                return true;
            }
        }

        public override bool TryGetTargetScaler(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out ITargetScaler targetScaler)
        {
            lock (this.initLock)
            {
                if (this.singletonDurableTaskMetricsProvider == null)
                {
                    this.singletonDurableTaskMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.storageAccountClientProvider,
                        this.logger);
                }

                targetScaler = new DurableTaskTargetScaler(functionId, this.singletonDurableTaskMetricsProvider, this, this.logger);
                return true;
            }
        }
    }
}
