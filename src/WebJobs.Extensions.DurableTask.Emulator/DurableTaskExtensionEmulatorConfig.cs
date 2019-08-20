// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration for the Durable Functions extension.
    /// </summary>
#if NETSTANDARD2_0
    [Extension("DurableTaskEmulator", "DurableTaskEmulator")]
#endif
    public class DurableTaskExtensionEmulatorConfig :
        DurableTaskExtensionBase,
        IExtensionConfigProvider
    {

        /// <inheritdoc />
        public DurableTaskExtensionEmulatorConfig(IOptions<DurableTaskEmulatorOptions> options,
            ILoggerFactory loggerFactory,
            INameResolver nameResolver,
            IOrchestrationServiceFactory orchestrationServiceFactory,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandlerFactory = null,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper = null)
            : base(options.Value, loggerFactory, nameResolver, orchestrationServiceFactory, durableHttpMessageHandlerFactory, lifeCycleNotificationHelper)
        {
        }

        /// <inheritdoc />
        internal override DurableTaskOptions GetDefaultDurableTaskOptions()
        {
            return new DurableTaskEmulatorOptions();
        }

        /// <inheritdoc />
        internal override IDurableSpecialOperationsClient GetSpecialtyClient(TaskHubClient client)
        {
            return new DefaultDurableSpecialOperationsClient("Emulator");
        }

        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            base.Initialize(context);
        }
    }
}
