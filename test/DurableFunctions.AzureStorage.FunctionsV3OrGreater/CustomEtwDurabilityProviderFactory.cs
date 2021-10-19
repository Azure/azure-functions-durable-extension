// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class CustomEtwDurabilityProviderFactory : EmulatorDurabilityProviderFactory
    {
        public CustomEtwDurabilityProviderFactory()
            : base()
        {
        }

        public override string Name => typeof(CustomEtwDurabilityProviderFactory).Name;

        public override DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            return this.GetDurabilityProvider();
        }

        public override DurabilityProvider GetDurabilityProvider()
        {
            DurabilityProvider provider = base.GetDurabilityProvider();
            provider.EventSourceName = "DurableTask-CustomSource";
            EtwSource.Current.Information("Created durability provider.");
            return provider;
        }
    }

    [EventSource(Name = "DurableTask-CustomSource")]
#pragma warning disable SA1402 // File may only contain a single type
    internal sealed class EtwSource : EventSource
#pragma warning restore SA1402 // File may only contain a single type
    {
        public static readonly EtwSource Current = new EtwSource();

        [Event(1)]
        public void Information(string summary)
        {
            this.WriteEvent(1, summary);
        }
    }
}