// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// A telemetry channel that forwards telemetry to a channel owned by the Functions host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Durable distributed tracing keeps its own <see cref="TelemetryConfiguration"/> so that its
    /// telemetry initializers, processors and modules stay isolated from the host pipeline. That
    /// private configuration has no Microsoft Entra credential of its own, so when the Application
    /// Insights component has local authentication disabled its telemetry is rejected at ingestion.
    /// </para>
    /// <para>
    /// The host's channel already carries the credential and the Entra ingestion endpoint, so
    /// forwarding to it reuses the host's authentication without Durable ever creating, reading or
    /// referencing an <c>Azure.Core.TokenCredential</c>. That matters because the Application
    /// Insights SDK resolves that type in the host load context, and a credential created in the
    /// function app load context is rejected.
    /// </para>
    /// <para>
    /// This channel deliberately does not own the wrapped channel. It never disposes it, and it
    /// keeps <see cref="EndpointAddress"/> and <see cref="DeveloperMode"/> local so that
    /// configuring the Durable configuration cannot mutate host state.
    /// </para>
    /// </remarks>
    internal sealed class HostForwardingTelemetryChannel : ITelemetryChannel, IAsyncFlushable
    {
        private readonly ITelemetryChannel hostChannel;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostForwardingTelemetryChannel"/> class.
        /// </summary>
        /// <param name="hostChannel">The host-owned channel to forward telemetry to.</param>
        public HostForwardingTelemetryChannel(ITelemetryChannel hostChannel)
        {
            this.hostChannel = hostChannel ?? throw new ArgumentNullException(nameof(hostChannel));
        }

        /// <summary>
        /// Gets or sets a value indicating whether this channel is in developer mode.
        /// </summary>
        /// <remarks>
        /// Kept local. Forwarding this would let the Durable configuration change how the host
        /// batches its own telemetry.
        /// </remarks>
        public bool? DeveloperMode { get; set; }

        /// <summary>
        /// Gets or sets the channel's endpoint.
        /// </summary>
        /// <remarks>
        /// Kept local. The host channel already points at the Entra ingestion endpoint, and the
        /// Durable configuration has no credential of its own, so propagating this value would
        /// downgrade the host to the non-Entra endpoint.
        /// </remarks>
        public string EndpointAddress { get; set; }

        /// <summary>
        /// Gets the host-owned channel that telemetry is forwarded to.
        /// </summary>
        internal ITelemetryChannel HostChannel => this.hostChannel;

        /// <summary>
        /// Forwards a telemetry item to the host channel, which transmits it using the host's
        /// credential and endpoint.
        /// </summary>
        /// <param name="item">The telemetry item to send.</param>
        public void Send(ITelemetry item) => this.hostChannel.Send(item);

        /// <summary>
        /// Flushes the host channel.
        /// </summary>
        public void Flush() => this.hostChannel.Flush();

        /// <summary>
        /// Asynchronously flushes the host channel when supported.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The host channel's asynchronous flush result, or false after a synchronous flush.</returns>
        public Task<bool> FlushAsync(CancellationToken cancellationToken)
        {
            if (this.hostChannel is IAsyncFlushable asyncFlushable)
            {
                return asyncFlushable.FlushAsync(cancellationToken);
            }

            this.hostChannel.Flush();
            return Task.FromResult(false);
        }

        /// <summary>
        /// Does nothing. The host owns the wrapped channel's lifetime.
        /// </summary>
        public void Dispose()
        {
            // Intentionally empty: disposing the host channel here would stop host telemetry.
        }
    }
}
