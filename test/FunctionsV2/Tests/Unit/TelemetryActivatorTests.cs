// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    [Collection("Non-Parallel Collection")]
    public class TelemetryActivatorTests
    {
        private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westus2-0.in.applicationinsights.azure.com/";
        private static readonly ActivitySource TestActivitySource = new ActivitySource("WebJobs.Extensions.DurableTask.Tests");

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Constructors_PreserveLegacySignature_AndRequireExplicitHostConfiguration()
        {
            ConstructorInfo legacyConstructor = typeof(TelemetryActivator).GetConstructor(new[]
            {
                typeof(IOptions<DurableTaskOptions>),
                typeof(INameResolver),
            });
            ConstructorInfo hostConfigurationConstructor = typeof(TelemetryActivator).GetConstructor(new[]
            {
                typeof(IOptions<DurableTaskOptions>),
                typeof(INameResolver),
                typeof(TelemetryConfiguration),
            });

            Assert.NotNull(legacyConstructor);
            Assert.NotNull(hostConfigurationConstructor);
            Assert.False(hostConfigurationConstructor.GetParameters()[2].IsOptional);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ResolveFromDi_UsesRegisteredTelemetryConfiguration()
        {
            var channel = new CollectingTelemetryChannel();
            var hostTelemetryConfiguration = TelemetryConfiguration.CreateDefault();
            hostTelemetryConfiguration.TelemetryChannel = channel;

            using ServiceProvider serviceProvider = CreateServiceProvider(hostTelemetryConfiguration);
            var activator = Assert.IsType<TelemetryActivator>(serviceProvider.GetRequiredService<ITelemetryActivator>());

            Assert.Same(hostTelemetryConfiguration, GetHostTelemetryConfiguration(activator));

            activator.Initialize(NullLogger.Instance);
            EmitDurableActivity("orchestration:host-config", ActivityKind.Server, "orchestration");
            EmitDurableActivity("activity:host-config", ActivityKind.Internal, "activity");

            Assert.Contains(channel.Items.OfType<RequestTelemetry>(), telemetry => telemetry.Name == "orchestration:host-config");
            Assert.Contains(channel.Items.OfType<DependencyTelemetry>(), telemetry => telemetry.Name == "activity:host-config");
            Assert.Single(hostTelemetryConfiguration.TelemetryInitializers.OfType<DurableTaskInstanceIdTelemetryInitializer>());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ResolveFromDi_FallsBackToLegacyConstructor_WhenTelemetryConfigurationMissing()
        {
            var sentItems = new ConcurrentQueue<ITelemetry>();

            using ServiceProvider serviceProvider = CreateServiceProvider();
            var activator = Assert.IsType<TelemetryActivator>(serviceProvider.GetRequiredService<ITelemetryActivator>());

            Assert.Null(GetHostTelemetryConfiguration(activator));

            activator.OnSend = telemetry => sentItems.Enqueue(telemetry);
            activator.Initialize(NullLogger.Instance);
            EmitDurableActivity("orchestration:di-fallback", ActivityKind.Server, "orchestration");
            EmitDurableActivity("activity:di-fallback", ActivityKind.Internal, "activity");

            Assert.Contains(sentItems.OfType<RequestTelemetry>(), telemetry => telemetry.Name == "orchestration:di-fallback");
            Assert.Contains(sentItems.OfType<DependencyTelemetry>(), telemetry => telemetry.Name == "activity:di-fallback");
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_PrefersOnSendTestHook_OverRegisteredTelemetryConfiguration()
        {
            var channel = new CollectingTelemetryChannel();
            var hostTelemetryConfiguration = TelemetryConfiguration.CreateDefault();
            hostTelemetryConfiguration.TelemetryChannel = channel;
            var sentItems = new ConcurrentQueue<ITelemetry>();

            using var activator = new TelemetryActivator(CreateOptions(), CreateNameResolver(), hostTelemetryConfiguration)
            {
                OnSend = telemetry => sentItems.Enqueue(telemetry),
            };

            activator.Initialize(NullLogger.Instance);
            EmitDurableActivity("orchestration:on-send", ActivityKind.Server, "orchestration");

            Assert.Contains(sentItems.OfType<RequestTelemetry>(), telemetry => telemetry.Name == "orchestration:on-send");
            Assert.Empty(channel.Items);
        }

        private static IOptions<DurableTaskOptions> CreateOptions()
        {
            return Options.Create(new DurableTaskOptions
            {
                Tracing = new Microsoft.Azure.WebJobs.Extensions.DurableTask.TraceOptions
                {
                    DistributedTracingEnabled = true,
                    Version = DurableDistributedTracingVersion.V2,
                },
            });
        }

        private static INameResolver CreateNameResolver()
        {
            return new SimpleNameResolver(new Dictionary<string, string>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = ConnectionString,
            });
        }

        private static ServiceProvider CreateServiceProvider(TelemetryConfiguration telemetryConfiguration = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(CreateOptions());
            services.AddSingleton(CreateNameResolver());

            if (telemetryConfiguration != null)
            {
                services.AddSingleton(telemetryConfiguration);
            }

            services.AddSingleton<ITelemetryActivator, TelemetryActivator>();
            return services.BuildServiceProvider();
        }

        private static TelemetryConfiguration GetHostTelemetryConfiguration(TelemetryActivator activator)
        {
            FieldInfo field = typeof(TelemetryActivator).GetField("hostTelemetryConfiguration", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(activator) as TelemetryConfiguration;
        }

        private static void EmitDurableActivity(string name, ActivityKind kind, string durableTaskType)
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            using Activity activity = TestActivitySource.StartActivity(name, kind);
            activity?.SetTag("durabletask.type", durableTaskType);
            activity?.SetTag("durabletask.task.operation", $"test_{durableTaskType}");
            activity?.SetTag("durabletask.task.instance_id", "test-instance");
        }

        private sealed class CollectingTelemetryChannel : ITelemetryChannel
        {
            public ConcurrentQueue<ITelemetry> Items { get; } = new ConcurrentQueue<ITelemetry>();

            public bool? DeveloperMode { get; set; }

            public string EndpointAddress { get; set; }

            public void Send(ITelemetry item)
            {
                this.Items.Enqueue(item);
            }

            public void Flush()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
