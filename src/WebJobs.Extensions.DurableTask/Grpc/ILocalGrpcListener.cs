// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc
{
    internal enum LocalGrpcListenerMode
    {
        Default = 0,
        Legacy = 1,
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
    }

    internal static class LocalGrpcListener
    {
        public static ILocalGrpcListener Create(DurableTaskExtension extension, LocalGrpcListenerMode mode)
        {
            return mode switch
            {
                LocalGrpcListenerMode.Default or LocalGrpcListenerMode.AspNetCore => new AspNetCoreLocalGrpcListener(extension),
                _ => new LegacyLocalGrpcListener(extension),
            };
        }
    }
}
