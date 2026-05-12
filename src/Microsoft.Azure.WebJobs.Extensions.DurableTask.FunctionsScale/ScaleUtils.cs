// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// Provides helper methods for resolving scale monitors and target scalers from a <see cref="ScalabilityProvider"/>.
    /// </summary>
    public static class ScaleUtils
    {
        /// <summary>
        /// Resolves an <see cref="IScaleMonitor"/> for the specified Durable Task trigger
        /// using the provided scalability provider.
        /// </summary>
        /// <param name="scalabilityProvider">
        /// The scalability provider used to obtain backend-specific scale monitoring support.
        /// </param>
        /// <param name="functionId">
        /// The unique identifier of the function.
        /// </param>
        /// <param name="functionName">
        /// The name of the function.
        /// </param>
        /// <param name="connectionName">
        /// The name of the storage connection, if specified.
        /// </param>
        /// <param name="hubName">
        /// The Durable Task hub name.
        /// </param>
        /// <returns>
        /// An <see cref="IScaleMonitor"/> instance when scale monitoring is supported;
        /// otherwise, a no-op scale monitor.
        /// </returns>
        public static IScaleMonitor GetScaleMonitor(
            ScalabilityProvider scalabilityProvider,
            string functionId,
            FunctionName functionName,
            string? connectionName,
            string hubName)
        {
            return scalabilityProvider.TryGetScaleMonitor(
                    functionId,
                    functionName.Name,
                    hubName,
                    connectionName ?? string.Empty,
                    out IScaleMonitor scaleMonitor)
                ? scaleMonitor
                : new NoOpScaleMonitor(
                    $"{functionId}-DurableTaskTrigger-{hubName}".ToLower(),
                    functionId);
        }

        /// <summary>
        /// A placeholder scale monitor, can be used by durability providers that do not support runtime scaling.
        /// This is required to allow operation of those providers even if runtime scaling is turned off
        /// see discussion https://github.com/Azure/azure-functions-durable-extension/pull/1009/files#r341767018.
        /// </summary>
        internal sealed class NoOpScaleMonitor : IScaleMonitor
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NoOpScaleMonitor"/> class.
            /// </summary>
            /// <param name="name">A descriptive name.</param>
            /// <param name="functionId">The function ID.</param>
            public NoOpScaleMonitor(string name, string functionId)
            {
                this.Descriptor = new ScaleMonitorDescriptor(name, functionId);
            }

            /// <summary>
            /// Gets a descriptive name.
            /// </summary>
            public ScaleMonitorDescriptor Descriptor { get; private set; }

            /// <inheritdoc/>
            Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
            {
                throw new InvalidOperationException("The current DurableTask backend configuration does not support runtime scaling");
            }

            /// <inheritdoc/>
            ScaleStatus IScaleMonitor.GetScaleStatus(ScaleStatusContext context)
            {
                throw new InvalidOperationException("The current DurableTask backend configuration does not support runtime scaling");
            }
        }

        /// <summary>
        /// Resolves an <see cref="ITargetScaler"/> for the specified Durable Task trigger
        /// using the provided scalability provider.
        /// </summary>
        /// <param name="scalabilityProvider">
        /// The scalability provider used to obtain backend-specific target scaling support.
        /// </param>
        /// <param name="functionId">
        /// The unique identifier of the function.
        /// </param>
        /// <param name="functionName">
        /// The name of the function.
        /// </param>
        /// <param name="connectionName">
        /// The name of the storage connection, if specified.
        /// </param>
        /// <param name="hubName">
        /// The Durable Task hub name.
        /// </param>
        /// <returns>
        /// An <see cref="ITargetScaler"/> instance when target-based scaling is supported;
        /// otherwise, a no-op target scaler.
        /// </returns>
#pragma warning disable SA1201 // Elements should appear in the correct order
        public static ITargetScaler GetTargetScaler(ScalabilityProvider scalabilityProvider, string functionId, FunctionName functionName, string? connectionName, string hubName)
#pragma warning restore SA1201 // Elements should appear in the correct order
        {
            return scalabilityProvider.TryGetTargetScaler(
                functionId,
                functionName.Name,
                hubName,
                connectionName ?? string.Empty,
                out ITargetScaler targetScaler)
            ? targetScaler
            : new NoOpTargetScaler(functionId);
        }

        internal sealed class NoOpTargetScaler : ITargetScaler
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NoOpTargetScaler"/> class.
            /// </summary>
            /// <param name="functionId">The function ID.</param>
            public NoOpTargetScaler(string functionId)
            {
                this.TargetScalerDescriptor = new TargetScalerDescriptor(functionId);
            }

            public TargetScalerDescriptor TargetScalerDescriptor { get; }

            public Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
            {
                throw new NotSupportedException("The current DurableTask backend configuration does not support target-based scaling");
            }
        }
    }
}
