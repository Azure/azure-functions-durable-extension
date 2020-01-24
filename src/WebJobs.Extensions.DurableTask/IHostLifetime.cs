// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Service interface for signaling the extension when the host is starting up or shutting down.
    /// </summary>
    /// <remarks>
    /// This interface is expected to be used as an injected service.
    /// </remarks>
    public interface IHostLifetime
    {
        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that can be used to detect host startup events.
        /// </summary>
        /// <value>
        /// A <see cref="CancellationToken"/> that is signalled when the host has started up.
        /// </value>
        CancellationToken OnStarted { get; }

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that can be used to detect host stopping events.
        /// </summary>
        /// <value>
        /// A <see cref="CancellationToken"/> that is signalled when the host is beginning to shut down.
        /// </value>
        CancellationToken OnStopping { get; }

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that can be used to detect host shutdown events.
        /// </summary>
        /// <value>
        /// A <see cref="CancellationToken"/> that is signalled when the host has completed shutting down.
        /// </value>
        CancellationToken OnStopped { get; }
    }
}
