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

        public CancellationToken OnStarted => throw new NotImplementedException();

        public CancellationToken OnStopping => throw new NotImplementedException();

        public void SignalShutdown() => this.cts.Cancel();
    }
}
