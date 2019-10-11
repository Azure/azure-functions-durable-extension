// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using DurableTask.Emulator;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EmulatorOrchestrationServiceFactory : IDurabilityProviderFactory
    {
        private readonly DurabilityProvider provider;

        public EmulatorOrchestrationServiceFactory()
        {
            var service = new LocalOrchestrationService();
            this.provider = new DurabilityProvider("emulator", service, service);
        }

        public bool SupportsEntities => false;

        public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            return this.provider;
        }

        public DurabilityProvider GetDurabilityProvider()
        {
            return this.provider;
        }
    }
}
