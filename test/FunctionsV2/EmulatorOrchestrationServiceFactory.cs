// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using DurableTask.Emulator;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EmulatorOrchestrationServiceFactory : IOrchestrationServiceFactory
    {
        private readonly LocalOrchestrationService service;

        public EmulatorOrchestrationServiceFactory()
        {
            this.service = new LocalOrchestrationService();
        }

        public bool SupportsEntities => false;

        public IOrchestrationServiceClient GetOrchestrationClient(DurableClientAttribute attribute)
        {
            return (IOrchestrationServiceClient)this.service;
        }

        public IOrchestrationService GetOrchestrationService()
        {
            return (IOrchestrationService)this.service;
        }

        public IDurableSpecialOperationsClient GetSpecialtyClient(TaskHubClient client)
        {
            return new DefaultDurableSpecialOperationsClient("Emulator");
        }
    }
}
