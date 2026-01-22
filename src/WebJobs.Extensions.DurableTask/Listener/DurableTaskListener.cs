// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class DurableTaskListener : IListener, IScaleMonitorProvider, ITargetScalerProvider
    {
        private readonly DurableTaskExtension config;
        private readonly string functionId;
        private readonly FunctionName functionName;
        private readonly FunctionType functionType;
        private readonly string connectionName;

        private readonly Lazy<IScaleMonitor> scaleMonitor;
        private readonly Lazy<ITargetScaler> targetScaler;

        public DurableTaskListener(
            DurableTaskExtension config,
            string functionId,
            FunctionName functionName,
            FunctionType functionType,
            string connectionName)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            if (functionName == default(FunctionName))
            {
                throw new ArgumentNullException(nameof(functionName));
            }

            this.functionId = functionId;
            this.functionName = functionName;
            this.functionType = functionType;
            this.connectionName = connectionName;

            // Lazily initialize scale monitor and target scaler using the Scale package
            this.scaleMonitor = new Lazy<IScaleMonitor>(() => this.CreateScaleMonitor());
            this.targetScaler = new Lazy<ITargetScaler>(() => this.CreateTargetScaler());
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return this.config.StartTaskHubWorkerIfNotStartedAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // The actual listener is a task hub worker, which is shared by all orchestration
            // and activity function listeners in the function app. The task hub worker
            // gets shut down only when all durable functions are shut down.
            switch (this.functionType)
            {
                case FunctionType.Orchestrator:
                    this.config.DeregisterOrchestrator(this.functionName);
                    break;
                case FunctionType.Entity:
                    this.config.DeregisterEntity(this.functionName);
                    break;
                case FunctionType.Activity:
                    this.config.DeregisterActivity(this.functionName);
                    break;
            }

            return this.config.StopTaskHubWorkerIfIdleAsync();
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public IScaleMonitor GetMonitor()
        {
            return this.scaleMonitor.Value;
        }

        /// <inheritdoc/>
        public ITargetScaler GetTargetScaler()
        {
            return this.targetScaler.Value;
        }

        private IScaleMonitor CreateScaleMonitor()
        {
            // Get the scalability provider from the Scale package
            ScalabilityProvider scalabilityProvider = this.GetScalabilityProvider();

            // Convert FunctionName to Scale.FunctionName
            var scaleFunctionName = new Scale.FunctionName(this.functionName.Name);

            return ScaleUtils.GetScaleMonitor(
                scalabilityProvider,
                this.functionId,
                scaleFunctionName,
                this.connectionName,
                this.config.Options.HubName);
        }

        private ITargetScaler CreateTargetScaler()
        {
            // Get the scalability provider from the Scale package
            ScalabilityProvider scalabilityProvider = this.GetScalabilityProvider();

            // Convert FunctionName to Scale.FunctionName
            var scaleFunctionName = new Scale.FunctionName(this.functionName.Name);

            return ScaleUtils.GetTargetScaler(
                scalabilityProvider,
                this.functionId,
                scaleFunctionName,
                this.connectionName,
                this.config.Options.HubName);
        }

        private ScalabilityProvider GetScalabilityProvider()
        {
            // Get the appropriate scalability provider factory based on the storage provider type
            IEnumerable<IScalabilityProviderFactory> factories = this.config.GetScalabilityProviderFactories();
            if (factories == null || !factories.Any())
            {
                throw new InvalidOperationException(
                    "No scalability provider factories registered. " +
                    "Ensure that AddDurableTask() was called during host startup.");
            }

            // Build metadata from DurableTaskOptions
            DurableTaskMetadata metadata = this.BuildMetadataFromOptions();

            // Get the factory for the configured storage provider
            IScalabilityProviderFactory factory = DurableTaskScaleExtension.GetScalabilityProviderFactory(
                metadata,
                this.config.GetLogger(),
                factories);

            // Create the scalability provider using the factory
            // Pass null for TriggerMetadata since we're in the host (not Scale Controller)
            return factory.GetScalabilityProvider(metadata, triggerMetadata: null);
        }

        private DurableTaskMetadata BuildMetadataFromOptions()
        {
            DurableTaskOptions options = this.config.Options;

            return new DurableTaskMetadata
            {
                TaskHubName = options.HubName,
                MaxConcurrentOrchestratorFunctions = options.MaxConcurrentOrchestratorFunctions,
                MaxConcurrentActivityFunctions = options.MaxConcurrentActivityFunctions,
                StorageProvider = options.StorageProvider,
            };
        }
    }
}
