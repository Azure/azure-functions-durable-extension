using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class TestDurableTaskExtension : DurableTaskExtension
    {
        public TestDurableTaskExtension(
            IOptions<DurableTaskOptions> options,
            ILoggerFactory loggerFactory,
            INameResolver nameResolver,
            IEnumerable<IDurabilityProviderFactory> orchestrationServiceFactories,
            IApplicationLifetimeWrapper hostLifetimeService,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandlerFactory = null,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper = null,
            IMessageSerializerSettingsFactory messageSerializerSettingsFactory = null,
#pragma warning disable CS0612 // Type or member is obsolete
            IPlatformInformation platformInformationService = null,
#pragma warning restore CS0612 // Type or member is obsolete
            IErrorSerializerSettingsFactory errorSerializerSettingsFactory = null,
#pragma warning disable CS0618 // Type or member is obsolete
            IWebHookProvider webhookProvider = null,
#pragma warning restore CS0618 // Type or member is obsolete
            ITelemetryActivator telemetryActivator = null)
            : base(
                    options,
                    loggerFactory,
                    nameResolver,
                    orchestrationServiceFactories,
                    hostLifetimeService,
                    durableHttpMessageHandlerFactory,
                    lifeCycleNotificationHelper,
                    messageSerializerSettingsFactory,
                    platformInformationService,
                    errorSerializerSettingsFactory,
                    webhookProvider,
                    telemetryActivator)
        {
            // Access DefaultDurabilityProvider to ensure it gets initialized during tests
            _ = this.DefaultDurabilityProvider;
        }
    }
}
