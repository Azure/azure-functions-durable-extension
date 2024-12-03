// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    internal static class ScaleUtils
    {
        internal static IScaleMonitor GetScaleMonitor(DurabilityProvider durabilityProvider, string functionId, FunctionName functionName, string? connectionName, string hubName)
        {
            if (durabilityProvider.TryGetScaleMonitor(
                    functionId,
                    functionName.Name,
                    hubName,
                    connectionName,
                    out IScaleMonitor scaleMonitor))
            {
                return scaleMonitor;
            }
            else
            {
                // the durability provider does not support runtime scaling.
                // Create an empty scale monitor to avoid exceptions (unless runtime scaling is actually turned on).
                return new NoOpScaleMonitor($"{functionId}-DurableTaskTrigger-{hubName}".ToLower(), functionId);
            }
        }

        /// <summary>
        /// A placeholder scale monitor, can be used by durability providers that do not support runtime scaling.
        /// This is required to allow operation of those providers even if runtime scaling is turned off
        /// see discussion https://github.com/Azure/azure-functions-durable-extension/pull/1009/files#r341767018.
        /// </summary>
        internal sealed class NoOpScaleMonitor : IScaleMonitor
        {
            /// <summary>
            /// Construct a placeholder scale monitor.
            /// </summary>
            /// <param name="name">A descriptive name.</param>
            /// <param name="functionId">The function ID.</param>
            public NoOpScaleMonitor(string name, string functionId)
            {
                this.Descriptor = new ScaleMonitorDescriptor(name, functionId);
            }

            /// <summary>
            /// A descriptive name.
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

#pragma warning disable SA1201 // Elements should appear in the correct order
        internal static ITargetScaler GetTargetScaler(DurabilityProvider durabilityProvider, string functionId, FunctionName functionName, string? connectionName, string hubName)
#pragma warning restore SA1201 // Elements should appear in the correct order
        {
            if (durabilityProvider.TryGetTargetScaler(
                    functionId,
                    functionName.Name,
                    hubName,
                    connectionName,
                    out ITargetScaler targetScaler))
            {
                return targetScaler;
            }
            else
            {
                // the durability provider does not support target-based scaling.
                // Create an empty target scaler to avoid exceptions (unless target-based scaling is actually turned on).
                return new NoOpTargetScaler(functionId);
            }
        }

        internal sealed class NoOpTargetScaler : ITargetScaler
        {
            /// <summary>
            /// Construct a placeholder target scaler.
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
