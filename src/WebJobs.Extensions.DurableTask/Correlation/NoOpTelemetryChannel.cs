// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// A stub of <see cref="ITelemetryChannel"/>.
    /// </summary>
    public sealed class NoOpTelemetryChannel : ITelemetryChannel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoOpTelemetryChannel"/> class.
        /// </summary>
        public NoOpTelemetryChannel()
        {
            this.OnSend = telemetry => { };
            this.OnFlush = () => { };
            this.OnDispose = () => { };
        }

        /// <summary>
        /// Gets or sets a value indicating whether this channel is in developer mode.
        /// </summary>
        public bool? DeveloperMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the channel's URI. To this URI the telemetry is expected to be sent.
        /// </summary>
        public string EndpointAddress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to throw an error.
        /// </summary>
        public bool ThrowError { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked by the <see cref="Send"/> method.
        /// </summary>
        public Action<ITelemetry> OnSend { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked by the <see cref="Flush"/> method.
        /// </summary>
        public Action OnFlush { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked by the <see cref="Dispose"/> method.
        /// </summary>
        public Action OnDispose { get; set; }

        /// <summary>
        /// Implements the <see cref="ITelemetryChannel.Send"/> method by invoking the <see cref="OnSend"/> callback.
        /// </summary>
        public void Send(ITelemetry item)
        {
            if (this.ThrowError)
            {
                throw new Exception("test error");
            }

            this.OnSend(item);
        }

        /// <summary>
        /// Implements the <see cref="IDisposable.Dispose"/> method.
        /// </summary>
        public void Dispose()
        {
            this.OnDispose();
        }

        /// <summary>
        /// Implements  the <see cref="ITelemetryChannel.Flush" /> method.
        /// </summary>
        public void Flush()
        {
            this.OnFlush();
        }
    }
}
