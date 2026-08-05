// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
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
        public void Initialize_UsesProvidedHostTelemetryConfiguration_ForV2()
        {
            var channel = new CollectingTelemetryChannel();
            var hostTelemetryConfiguration = TelemetryConfiguration.CreateDefault();
            hostTelemetryConfiguration.TelemetryChannel = channel;

            using TelemetryActivator activator = CreateTelemetryActivator(hostTelemetryConfiguration);

            EmitDurableActivity("orchestration:host-config", ActivityKind.Server, "orchestration");
            EmitDurableActivity("activity:host-config", ActivityKind.Internal, "activity");

            Assert.Contains(channel.Items.OfType<RequestTelemetry>(), telemetry => telemetry.Name == "orchestration:host-config");
            Assert.Contains(channel.Items.OfType<DependencyTelemetry>(), telemetry => telemetry.Name == "activity:host-config");
            Assert.Single(hostTelemetryConfiguration.TelemetryInitializers.OfType<DurableTaskInstanceIdTelemetryInitializer>());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_PrefersOnSendTestHook_OverProvidedHostTelemetryConfiguration()
        {
            var channel = new CollectingTelemetryChannel();
            var hostTelemetryConfiguration = TelemetryConfiguration.CreateDefault();
            hostTelemetryConfiguration.TelemetryChannel = channel;
            var sentItems = new ConcurrentQueue<ITelemetry>();

            using TelemetryActivator activator = CreateTelemetryActivator(hostTelemetryConfiguration, telemetry => sentItems.Enqueue(telemetry));

            EmitDurableActivity("orchestration:on-send", ActivityKind.Server, "orchestration");

            Assert.Contains(sentItems.OfType<RequestTelemetry>(), telemetry => telemetry.Name == "orchestration:on-send");
            Assert.Empty(channel.Items);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_StandaloneTelemetryConfiguration_PreservesOnSendBehavior()
        {
            var sentItems = new ConcurrentQueue<ITelemetry>();

            using TelemetryActivator activator = CreateTelemetryActivator(onSend: telemetry => sentItems.Enqueue(telemetry));

            EmitDurableActivity("orchestration:standalone", ActivityKind.Server, "orchestration");
            EmitDurableActivity("activity:standalone", ActivityKind.Internal, "activity");

            Assert.Contains(sentItems.OfType<RequestTelemetry>(), telemetry => telemetry.Name == "orchestration:standalone");
            Assert.Contains(sentItems.OfType<DependencyTelemetry>(), telemetry => telemetry.Name == "activity:standalone");
        }

        private static TelemetryActivator CreateTelemetryActivator(TelemetryConfiguration telemetryConfiguration = null, Action<ITelemetry> onSend = null)
        {
            var options = Options.Create(new DurableTaskOptions
            {
                Tracing = new Microsoft.Azure.WebJobs.Extensions.DurableTask.TraceOptions
                {
                    DistributedTracingEnabled = true,
                    Version = DurableDistributedTracingVersion.V2,
                },
            });

            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = ConnectionString,
            });

            var activator = new TelemetryActivator(options, nameResolver, telemetryConfiguration)
            {
                OnSend = onSend,
            };

            activator.Initialize(NullLogger.Instance);
            return activator;
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
