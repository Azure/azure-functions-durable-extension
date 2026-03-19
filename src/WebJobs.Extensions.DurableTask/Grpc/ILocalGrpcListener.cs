// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc
{
    internal enum LocalGrpcListenerMode
    {
        Default = 0,
        Legacy = 1, // Deprecated, AspNetCore will be used instead
        AspNetCore = 2,
    }

    /// <summary>
    /// A local listener for gRPC communication between host and worker.
    /// </summary>
    internal interface ILocalGrpcListener : IHostedService
    {
        /// <summary>
        /// Gets the address this listener is listening to.
        /// </summary>
        string? ListenAddress { get; }

        /// <summary>
        /// Gets a value indicating whether the gRPC listener is healthy (started and responsive).
        /// </summary>
        bool IsHealthy { get; }

        /// <summary>
        /// Ensures the gRPC listener is started and healthy, restarting it if necessary.
        /// </summary>
        Task EnsureStartedAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Waits for the <see cref="ListenAddress"/> to become available, with a timeout.
        /// Returns the listen address, or null if the timeout expires.
        /// </summary>
        Task<string?> WaitForListenAddressAsync(TimeSpan timeout, CancellationToken cancellationToken);
    }

    internal static class LocalGrpcListener
    {
        public static ILocalGrpcListener Create(DurableTaskExtension extension, LocalGrpcListenerMode mode)
        {
            if (mode == LocalGrpcListenerMode.Legacy)
            {
                extension.TraceHelper.ExtensionWarningEvent(
                    hubName: "",
                    functionName: "",
                    instanceId: "",
                    "The 'Legacy' GrpcListenerMode is removed, 'AspNetCore' mode will be used instead.");
            }

            return new AspNetCoreLocalGrpcListener(extension);
        }
    }
}
