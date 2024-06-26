// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class HostLifecycleService : IApplicationLifetimeWrapper
    {
        internal static readonly IApplicationLifetimeWrapper NoOp = new NoOpLifetimeWrapper();
#pragma warning disable CS0618 // Type or member is obsolete (no alternatives in .NET Standard 2.0)
        private readonly IApplicationLifetime appLifetime;

        public HostLifecycleService(IApplicationLifetime appLifetime)
        {
            this.appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public CancellationToken OnStarted => this.appLifetime.ApplicationStarted;

        public CancellationToken OnStopping => this.appLifetime.ApplicationStopping;

        public CancellationToken OnStopped => this.appLifetime.ApplicationStopped;

        private class NoOpLifetimeWrapper : IApplicationLifetimeWrapper
        {
            public CancellationToken OnStarted => CancellationToken.None;

            public CancellationToken OnStopping => CancellationToken.None;

            public CancellationToken OnStopped => CancellationToken.None;
        }
    }
}
