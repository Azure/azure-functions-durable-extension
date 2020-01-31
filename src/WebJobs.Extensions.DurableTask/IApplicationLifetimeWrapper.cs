// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Custom service interface for signaling the extension when the function app is starting up or shutting down.
    /// </summary>
    /// <remarks>
    /// This interface is expected to be used as an injected service. We use a "wrapper" interface instead of
    /// directly using the "real" <c>IApplicationLifetime</c> interface so that we can have an injected service
    /// that is available in both .NET Core (Functions 2.0+) and .NET Framework (Functions 1.0).
    /// </remarks>
    public interface IApplicationLifetimeWrapper
    {
        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that can be used to detect function app startup events.
        /// </summary>
        /// <value>
        /// A <see cref="CancellationToken"/> that is signalled when the function app has started up.
        /// </value>
        CancellationToken OnStarted { get; }

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that can be used to detect function app stopping events.
        /// </summary>
        /// <value>
        /// A <see cref="CancellationToken"/> that is signalled when the function app is beginning to shut down.
        /// </value>
        CancellationToken OnStopping { get; }

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that can be used to detect function app shutdown events.
        /// </summary>
        /// <value>
        /// A <see cref="CancellationToken"/> that is signalled when the function app has completed shutting down.
        /// </value>
        CancellationToken OnStopped { get; }
    }
}
