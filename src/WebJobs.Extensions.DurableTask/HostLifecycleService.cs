// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
#if !FUNCTIONS_V1
using Microsoft.Extensions.Hosting;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
#if !FUNCTIONS_V1
    internal class HostLifecycleService : IApplicationLifetimeWrapper
#else
    internal class HostLifecycleService
#endif
    {
        internal static readonly IApplicationLifetimeWrapper NoOp = new NoOpLifetimeWrapper();
#if !FUNCTIONS_V1
        private readonly IApplicationLifetime appLifetime;

        public HostLifecycleService(IApplicationLifetime appLifetime)
        {
            this.appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
        }

        public CancellationToken OnStarted => this.appLifetime.ApplicationStarted;

        public CancellationToken OnStopping => this.appLifetime.ApplicationStopping;

        public CancellationToken OnStopped => this.appLifetime.ApplicationStopped;
#endif

        private class NoOpLifetimeWrapper : IApplicationLifetimeWrapper
        {
            public CancellationToken OnStarted => CancellationToken.None;

            public CancellationToken OnStopping => CancellationToken.None;

            public CancellationToken OnStopped => CancellationToken.None;
        }
    }
}
