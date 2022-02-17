using DurableTask.Netherite;
using DurableTask.Netherite.AzureFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using Xunit.Abstractions;

namespace DurableFunctions.NetheriteEndToEnd
{
    public class NetheriteTestHelper : TestHelpers
    {
        public NetheriteTestHelper(ITestOutputHelper output) 
            : base(output)
        {
        }

        public override void RegisterDurabilityFactory(IWebJobsBuilder builder, IOptions<DurableTaskOptions> options, Type durableProviderFactoryType = null)
        {
            builder.Services.AddSingleton<IDurabilityProviderFactory, NetheriteProviderFactory>();
            options.Value.UseGracefulShutdown = false;
            options.Value.StorageProvider["type"] = NetheriteProviderFactory.ProviderName;
            this.nameResolvers[options.Value].AddSetting("EventHubsConnection", "Memory");

            options.Value.StorageProvider[nameof(NetheriteOrchestrationServiceSettings.LogLevelLimit)] = LogLevel.Trace.ToString();
            options.Value.StorageProvider[nameof(NetheriteOrchestrationServiceSettings.StorageLogLevelLimit)] = LogLevel.Trace.ToString();
            options.Value.StorageProvider[nameof(NetheriteOrchestrationServiceSettings.TransportLogLevelLimit)] = LogLevel.Trace.ToString();
            options.Value.StorageProvider[nameof(NetheriteOrchestrationServiceSettings.EventLogLevelLimit)] = LogLevel.Trace.ToString();
            options.Value.StorageProvider[nameof(NetheriteOrchestrationServiceSettings.WorkItemLogLevelLimit)] = LogLevel.Trace.ToString();

            // blob tracing does not work with AzureStorageEmulator because the latter does not support append blobs
            // options.Value.StorageProvider["TraceToBlob"] = "true";

            options.Value.StorageProvider[nameof(NetheriteOrchestrationServiceSettings.CacheOrchestrationCursors)] = options.Value.ExtendedSessionsEnabled.ToString();
            
            builder.AddDurableTask(options);
            builder.Services.AddSingleton<IConnectionStringResolver, TestConnectionStringResolver>();
        }
    }
}
