// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
#if !FUNCTIONS_V1
using Microsoft.Extensions.Hosting;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class HostLifecycleService : IApplicationLifetimeWrapper
    {
#if !FUNCTIONS_V1
        private readonly IApplicationLifetime appLifetime;

        public HostLifecycleService(IApplicationLifetime appLifetime)
        {
            this.appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
        }

        public CancellationToken OnStarted => this.appLifetime.ApplicationStarted;

        public CancellationToken OnStopping => this.appLifetime.ApplicationStopping;

        public CancellationToken OnStopped => this.appLifetime.ApplicationStopped;
#else
        private readonly CancellationToken startedToken = CancellationToken.None;
        private readonly CancellationToken stoppingToken = CancellationToken.None;
        private readonly CancellationToken stoppedToken = CancellationToken.None;

        public CancellationToken OnStarted => this.startedToken;

        public CancellationToken OnStopping => this.stoppingToken;

        public CancellationToken OnStopped => this.stoppedToken;
#endif
    }
}
