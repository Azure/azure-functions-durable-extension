// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableTaskWorkerContext
    {
        private readonly DurableTaskExtensionBase listenerConfig;
        private readonly ExtensionConfigContext extensionContext;
        private readonly List<ObjectCreator<TaskOrchestration>> orchestrations;
        private readonly List<ObjectCreator<TaskActivity>> activities;

        public DurableTaskWorkerContext(DurableTaskExtensionBase configuration, ExtensionConfigContext extensionContext)
        {
            this.listenerConfig = configuration;
            this.extensionContext = extensionContext;
            this.orchestrations = new List<ObjectCreator<TaskOrchestration>>();
            this.activities = new List<ObjectCreator<TaskActivity>>();
        }

        public DurableTaskExtensionBase ListenerConfig => this.listenerConfig;

        public ExtensionConfigContext ExtensionContext => this.extensionContext;

        public IList<ObjectCreator<TaskOrchestration>> Orchestrations => this.orchestrations;

        public IList<ObjectCreator<TaskActivity>> Activities => this.activities;
    }
}
