// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestHostShutdownNotificationService : IApplicationLifetimeWrapper
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public CancellationToken OnStopped => this.cts.Token;

        public CancellationToken OnStarted => this.cts.Token;

        public CancellationToken OnStopping => this.cts.Token;

        public void SignalShutdown() => this.cts.Cancel();
    }
}
