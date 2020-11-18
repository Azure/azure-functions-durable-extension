// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class AzureStorageShortenedTimerDurabilityProviderFactory : AzureStorageDurabilityProviderFactory
    {
        public AzureStorageShortenedTimerDurabilityProviderFactory(
            IOptions<DurableTaskOptions> options,
            IConnectionStringResolver connectionStringResolver,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
            : base(options, connectionStringResolver, nameResolver, loggerFactory)
        {
        }

        public override DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            AzureStorageDurabilityProvider provider = base.GetDurabilityProvider(attribute) as AzureStorageDurabilityProvider;
            provider.MaximumDelayTime = TimeSpan.FromMinutes(1);
            provider.LongRunningTimerIntervalLength = TimeSpan.FromSeconds(25);
            return provider;
        }

        public override DurabilityProvider GetDurabilityProvider()
        {
            AzureStorageDurabilityProvider provider = base.GetDurabilityProvider() as AzureStorageDurabilityProvider;
            provider.MaximumDelayTime = TimeSpan.FromMinutes(1);
            provider.LongRunningTimerIntervalLength = TimeSpan.FromSeconds(25);
            return provider;
        }
    }
}