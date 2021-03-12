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
            ILoggerFactory loggerFactory,
#pragma warning disable CS0612 // Type or member is obsolete
            IPlatformInformationService platformInformationService)
#pragma warning restore CS0612 // Type or member is obsolete
            : base(options, connectionStringResolver, nameResolver, loggerFactory, platformInformationService)
        {
        }

        public override DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            AzureStorageDurabilityProvider provider = base.GetDurabilityProvider(attribute) as AzureStorageDurabilityProvider;
            provider.MaximumDelayTime = TimeSpan.FromSeconds(10);
            provider.LongRunningTimerIntervalLength = TimeSpan.FromSeconds(3);
            return provider;
        }

        public override DurabilityProvider GetDurabilityProvider()
        {
            AzureStorageDurabilityProvider provider = base.GetDurabilityProvider() as AzureStorageDurabilityProvider;
            provider.MaximumDelayTime = TimeSpan.FromSeconds(10);
            provider.LongRunningTimerIntervalLength = TimeSpan.FromSeconds(3);
            return provider;
        }
    }
}