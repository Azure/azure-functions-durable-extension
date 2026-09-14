// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Azure.Identity;
using DurableTask.Core;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Collection("Non-Parallel Collection")]
    [Trait("TestType", "E2E")]
    public class CorrelationEndToEndTests
    {
        private const string TestSiteName = "TestSite";
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public CorrelationEndToEndTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, "W3CTraceContext")]
        [InlineData(true, "HttpCorrelationProtocol")]
        [InlineData(true, "W3CTraceContext")]
        [InlineData(false, "HttpCorrelationProtocol")]
        public async Task SingleOrchestration_With_Activity(bool extendedSessions, string protocol)
        {
            string[] orchestrationFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            var result = await
                this.ExecuteOrchestrationWithExceptionAsync(
                    orchestrationFunctionNames,
                    "SingleOrchestration",
                    "world",
                    extendedSessions,
                    protocol);
            var actual = result.Item1;
            Assert.Equal(5, actual.Count);
            Assert.Empty(result.Item2);
            Assert.Equal(
                new (Type, string)[]
                {
                    (typeof(RequestTelemetry), $"{TraceConstants.Client}: "),
                    (typeof(DependencyTelemetry), TraceConstants.Client),
                    (typeof(RequestTelemetry), $"{TraceConstants.Orchestrator} SayHelloWithActivity"),
                    (typeof(DependencyTelemetry), $"{TraceConstants.Orchestrator} Hello"),
                    (typeof(RequestTelemetry), $"{TraceConstants.Activity} Hello"),
                }.ToList(), actual.Select(x => (x.GetType(), x.Name)).ToList());
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, "W3CTraceContext")]
        [InlineData(true, "HttpCorrelationProtocol")]
        [InlineData(true, "W3CTraceContext")]
        [InlineData(false, "HttpCorrelationProtocol")]
        public async Task CheckOperationName_RequestTelemetry_SingleOrchestration(bool extendedSessions, string protocol)
        {
            string[] orchestrationFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            var result = await
                this.ExecuteOrchestrationWithExceptionAsync(
                    orchestrationFunctionNames,
                    "SingleOrchestration",
                    "world",
                    extendedSessions,
                    protocol);

            var traceTelemetry = result.Item1;

            // Using actual.First() because there's only one Request Telemetry where the name is "DtActivity Hello"
            RequestTelemetry dtActivityReqTelemetry = traceTelemetry.First(x => x.GetType() == typeof(RequestTelemetry) && x.Name.Contains(TraceConstants.Activity)) as RequestTelemetry;
            Assert.Equal("Hello", dtActivityReqTelemetry.Context.Operation.Name);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DistributedTracingV2_DefaultOperationNames_ArePopulated()
        {
            string[] orchestrationFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            var result = await this.ExecuteOrchestrationWithExceptionAsync(
                orchestrationFunctionNames,
                "DistributedTracingV2OperationNames",
                "world",
                extendedSessions: false,
                protocol: "W3CTraceContext",
                version: DurableDistributedTracingVersion.V2);

            List<OperationTelemetry> durableTaskTelemetry = result.Item1
                .Where(telemetry =>
                    telemetry.Name.StartsWith($"{TraceActivityConstants.Orchestration}:", StringComparison.Ordinal) ||
                    telemetry.Name.StartsWith($"{TraceActivityConstants.Activity}:", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(durableTaskTelemetry);
            Assert.All(
                durableTaskTelemetry,
                telemetry => Assert.Equal(telemetry.Name, telemetry.Context.Operation.Name));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DurableTelemetryInitializers_PreserveRawV2Spans(bool forwardToHostChannel)
        {
            string[] baseline = null;
            foreach (bool useDurableInitializer in new[] { false, true })
            {
                var hostTelemetry = new ConcurrentQueue<ITelemetry>();
                var durableTelemetry = new ConcurrentQueue<ITelemetry>();
                var sentSpans = new ConcurrentQueue<(OperationTelemetry Telemetry, object Snapshot)>();
                void Capture(ITelemetry telemetry, ConcurrentQueue<ITelemetry> stream)
                {
                    if (telemetry is OperationTelemetry span)
                    {
                        sentSpans.Enqueue((span, CaptureRawSpan(span)));
                    }

                    stream.Enqueue(telemetry);
                }

                const string connectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000001";
                var resolver = new SimpleNameResolver(new Dictionary<string, string>
                {
                    ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = connectionString,
                    ["APPLICATIONINSIGHTS_AUTHENTICATION_STRING"] = forwardToHostChannel ? "Authorization=AAD" : null,
                });
                var options = new DurableTaskOptions
                {
                    Tracing = new TraceOptions
                    {
                        DistributedTracingEnabled = true,
                        Version = DurableDistributedTracingVersion.V2,
                    },
                };
                var sharedInitializer = new HostMetadataInitializer();
                using (var host = TestHelpers.GetJobHost(
                    this.loggerProvider,
                    nameof(this.DurableTelemetryInitializers_PreserveRawV2Spans),
                    enableExtendedSessions: false,
                    options: options,
                    nameResolver: resolver,
                    onSend: forwardToHostChannel ? null : telemetry => Capture(telemetry, durableTelemetry),
                    configureLogging: logging =>
                    {
                        logging.AddApplicationInsightsWebJobs(
                            ai =>
                            {
                                ai.ConnectionString = connectionString;
                                ai.EnableDependencyTracking = false;
                                ai.EnablePerformanceCountersCollection = false;
                                ai.EnableLiveMetrics = false;
                                ai.SamplingSettings = null;
                            },
                            configuration => configuration.TelemetryInitializers.Add(sharedInitializer));
                        logging.Services.AddSingleton<ITelemetryChannel>(
                            new NoOpTelemetryChannel { OnSend = telemetry => Capture(telemetry, hostTelemetry) });
                        if (useDurableInitializer)
                        {
                            logging.Services.AddDurableTaskTelemetryInitializer(sharedInitializer);
                        }
                    }))
                {
                    await host.StartAsync();
                    var success = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "world", this.output);
                    Assert.Equal(OrchestrationRuntimeStatus.Completed, (await success.WaitForCompletionAsync(this.output)).RuntimeStatus);
                    var failure = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ThrowOrchestrator), "expected failure", this.output);
                    Assert.Equal(OrchestrationRuntimeStatus.Failed, (await failure.WaitForCompletionAsync(this.output)).RuntimeStatus);
                    var entity = await host.StartOrchestratorAsync(
                        nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                        new EntityId("StringStore2", Guid.NewGuid().ToString()),
                        this.output);
                    Assert.Equal(OrchestrationRuntimeStatus.Completed, (await entity.WaitForCompletionAsync(this.output)).RuntimeStatus);
                    await host.StopAsync();
                }

                // Inspect both raw streams before any Durable-name filtering or correlation sorting.
                OperationTelemetry[] allSpans = hostTelemetry.Concat(durableTelemetry).OfType<OperationTelemetry>().ToArray();
                Assert.NotEmpty(hostTelemetry);
                Assert.Equal(19, allSpans.Length);
                Assert.Equal(forwardToHostChannel ? 19 : 5, hostTelemetry.OfType<OperationTelemetry>().Count());
                Assert.Equal(forwardToHostChannel ? 0 : 14, durableTelemetry.OfType<OperationTelemetry>().Count());
                Assert.Equal(allSpans.Length, sentSpans.Count);
                Assert.Equal(useDurableInitializer ? 19 : 5, sharedInitializer.BeforeEnrichment.Count);
                if (useDurableInitializer)
                {
                    // Match by object identity, not mutable/random IDs. Compare immutable values
                    // captured before enrichment with this same record at the channel boundary.
                    Assert.All(sentSpans, sent =>
                    {
                        Assert.True(sharedInitializer.BeforeEnrichment.TryGetValue(sent.Telemetry, out object before));
                        Assert.Equal(before, sent.Snapshot);
                    });
                }

                Assert.Equal(allSpans.Length, allSpans.Select(span => (span.Context.Operation.Id, span.Id)).Distinct().Count());
                Assert.All(allSpans, span =>
                {
                    Assert.False(string.IsNullOrEmpty(span.Id));
                    Assert.False(string.IsNullOrEmpty(span.Context.Operation.Id));
                });

                var signature = allSpans.Select(span => $"{span.GetType().Name}|{span.Name}|{span.Success}")
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                this.output.WriteLine(string.Join(Environment.NewLine, signature));
                foreach (var sent in sentSpans)
                {
                    this.output.WriteLine(sent.Snapshot.ToString());
                }

                if (baseline == null)
                {
                    baseline = signature;
                }
                else
                {
                    Assert.Equal(baseline, signature);
                }

                RequestTelemetry hello = Assert.Single(allSpans.OfType<RequestTelemetry>(), span => span.Name == "activity:Hello");
                Assert.True(hello.Success);
                RequestTelemetry failedActivity = Assert.Single(allSpans.OfType<RequestTelemetry>(), span => span.Name == "activity:ThrowActivity");
                Assert.False(failedActivity.Success);
                Assert.Single(allSpans.OfType<RequestTelemetry>(), span => span.Name == "orchestration:SayHelloWithActivity");
                Assert.False(Assert.Single(allSpans.OfType<RequestTelemetry>(), span => span.Name == "orchestration:ThrowOrchestrator").Success);
                Assert.Contains(allSpans, span => span.Name.StartsWith("entity:", StringComparison.Ordinal));
                AssertRawV2Topology(allSpans);

                foreach (var span in allSpans.Where(span =>
                    span.Name.StartsWith("orchestration:", StringComparison.Ordinal) ||
                    span.Name.StartsWith("activity:", StringComparison.Ordinal) ||
                    span.Name.StartsWith("entity:", StringComparison.Ordinal)))
                {
                    Assert.Equal(span.Name, span.Context.Operation.Name);
                    Assert.Equal(useDurableInitializer, span.Properties.ContainsKey("host-enrichment"));
                    if (useDurableInitializer)
                    {
                        Assert.Equal("orders-functions", span.Context.Cloud.RoleName);
                    }
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public void RawSpanSnapshot_DetectsChangedAndDroppedProducerFields(bool isDependency)
        {
            var mutations = new Dictionary<string, Action<OperationTelemetry, bool>>
            {
                ["Id"] = (span, clear) => span.Id = clear ? null : "changed-span",
                ["OperationId"] = (span, clear) => span.Context.Operation.Id = clear ? null : "changed-operation",
                ["ParentId"] = (span, clear) => span.Context.Operation.ParentId = clear ? null : "changed-parent",
                ["Timestamp"] = (span, clear) => span.Timestamp = clear ? default : span.Timestamp.AddTicks(1),
                ["Duration"] = (span, clear) => span.Duration = clear ? default : span.Duration.Add(TimeSpan.FromTicks(1)),
                ["Name"] = (span, clear) => span.Name = clear ? null : "changed-name",
                ["Success"] = (span, clear) => span.Success = clear ? null : !span.Success,
            };
            if (isDependency)
            {
                mutations["ResultCode"] = (span, clear) => ((DependencyTelemetry)span).ResultCode = clear ? null : "500";
                mutations["Type"] = (span, clear) => ((DependencyTelemetry)span).Type = clear ? null : "changed-type";
                mutations["Target"] = (span, clear) => ((DependencyTelemetry)span).Target = clear ? null : "changed-target";
                mutations["Data"] = (span, clear) => ((DependencyTelemetry)span).Data = clear ? null : "changed-data";
            }
            else
            {
                mutations["ResponseCode"] = (span, clear) => ((RequestTelemetry)span).ResponseCode = clear ? null : "500";
                mutations["Url"] = (span, clear) => ((RequestTelemetry)span).Url = clear ? null : new Uri("https://example.test/changed");
                mutations["Source"] = (span, clear) => ((RequestTelemetry)span).Source = clear ? null : "changed-source";
            }

            foreach (var mutation in mutations)
            {
                foreach (bool clear in new[] { false, true })
                {
                    OperationTelemetry span = isDependency
                        ? new DependencyTelemetry { ResultCode = "201", Type = "queue", Target = "orders", Data = "enqueue" }
                        : new RequestTelemetry { ResponseCode = "201", Url = new Uri("https://example.test/orders"), Source = "caller" };
                    span.Id = "1111111111111111";
                    span.Context.Operation.Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                    span.Context.Operation.ParentId = "bbbbbbbbbbbbbbbb";
                    span.Timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
                    span.Duration = TimeSpan.FromMilliseconds(123);
                    span.Name = "activity:Hello";
                    span.Success = true;
                    object before = CaptureRawSpan(span);

                    new HostMetadataInitializer().Initialize(span);
                    Assert.Equal(before, CaptureRawSpan(span));
                    mutation.Value(span, clear);

                    this.output.WriteLine($"Changed {mutation.Key} (clear: {clear})");
                    Assert.NotEqual(before, CaptureRawSpan(span));
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("Authorization=AAD", "system-assigned")]
        [InlineData("Authorization=AAD;ClientId=00000000-0000-0000-0000-000000000001", "user-assigned")]
        [InlineData(" authoRization=AAD ;; ClIentId = 00000000-0000-0000-0000-000000000001 ", "user-assigned")]
        public void TelemetryActivator_ValidAuthenticationString_EnablesEntraAuthentication(
            string authenticationString,
            string expectedIdentityType)
        {
            TraceOptions traceOptions = new TraceOptions()
            {
                DistributedTracingEnabled = true,
                Version = DurableDistributedTracingVersion.V2,
            };
            DurableTaskOptions options = new DurableTaskOptions
            {
                Tracing = traceOptions,
            };
            var mockNameResolver = GetNameResolverMock(
                new[]
                {
                    ("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=00000000-0000-0000-0000-000000000000"),
                    ("APPLICATIONINSIGHTS_AUTHENTICATION_STRING", authenticationString),
                });

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TelemetryActivator_ValidAuthenticationString_EnablesEntraAuthentication),
                enableExtendedSessions: false,
                nameResolver: mockNameResolver.Object,
                options: options))
            {
                string expectedMessage =
                    $"Microsoft Entra authentication enabled for Durable distributed tracing using the {expectedIdentityType} managed identity.";
                Assert.Contains(
                    this.loggerProvider.GetAllLogMessages(),
                    log => log.FormattedMessage?.StartsWith(expectedMessage, StringComparison.Ordinal) == true);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("Authorization=AAD1")]
        [InlineData("Auth123=AAD")]
        [InlineData("Authorization=AAD;ClientId=123")]
        public void TelemetryActivator_InvalidAuthenticationString_LogsWarning(string authenticationString)
        {
            TraceOptions traceOptions = new TraceOptions()
            {
                DistributedTracingEnabled = true,
                Version = DurableDistributedTracingVersion.V2,
            };
            DurableTaskOptions options = new DurableTaskOptions
            {
                Tracing = traceOptions,
            };
            var mockNameResolver = GetNameResolverMock(
                new[]
                {
                    ("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=00000000-0000-0000-0000-000000000000"),
                    ("APPLICATIONINSIGHTS_AUTHENTICATION_STRING", authenticationString),
                });

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TelemetryActivator_InvalidAuthenticationString_LogsWarning),
                enableExtendedSessions: false,
                nameResolver: mockNameResolver.Object,
                options: options))
            {
                const string expectedMessage =
                    "APPLICATIONINSIGHTS_AUTHENTICATION_STRING is invalid and will not be used for Durable Functions distributed tracing.";
                Assert.Contains(
                    this.loggerProvider.GetAllLogMessages(),
                    log => log.FormattedMessage?.StartsWith(expectedMessage, StringComparison.Ordinal) == true);
            }
        }

        /*
         * Reproduces the root cause of #3497 end to end. Durable keeps a private
         * TelemetryConfiguration, which has no Microsoft Entra credential of its own, so with
         * DisableLocalAuth=true its spans are rejected at ingestion. The fix forwards them to the
         * host channel, which already carries the host's credential. Capturing on that host channel
         * proves the spans really traverse it, and leaving onSend unset means the TelemetryActivator
         * comes purely from the production DI registration.
         */
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DistributedTracingV2_EntraAuthentication_ForwardsModuleSpansThroughHostChannel()
        {
            string[] orchestrationFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            using TelemetryConfiguration hostTelemetryConfiguration = TelemetryConfiguration.CreateDefault();
            TelemetryActivator activator = null;

            var result = await this.ExecuteOrchestrationWithExceptionAsync(
                orchestrationFunctionNames,
                "DTV2EntraForward",
                "world",
                extendedSessions: false,
                protocol: "W3CTraceContext",
                version: DurableDistributedTracingVersion.V2,
                authenticationString: "Authorization=AAD",
                hostTelemetryConfiguration: hostTelemetryConfiguration,
                inspectTelemetryActivator: telemetryActivator => activator = telemetryActivator,
                captureThroughHostChannel: true);

            // The activator must come from the production factory with the registered host configuration,
            // otherwise this test would silently degrade to the standalone credential path.
            Assert.NotNull(activator);
            FieldInfo hostConfigurationField = typeof(TelemetryActivator).GetField(
                "hostTelemetryConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(hostConfigurationField);
            Assert.Same(hostTelemetryConfiguration, hostConfigurationField.GetValue(activator));

            var forwardingChannel = Assert.IsType<HostForwardingTelemetryChannel>(
                activator.TelemetryConfiguration.TelemetryChannel);
            Assert.Same(hostTelemetryConfiguration.TelemetryChannel, forwardingChannel.HostChannel);

            const string expectedMessage =
                "Microsoft Entra authentication enabled for Durable distributed tracing using the system-assigned managed identity.";
            Assert.Contains(
                this.loggerProvider.GetAllLogMessages(),
                log => log.FormattedMessage?.StartsWith(expectedMessage, StringComparison.Ordinal) == true);

            const string fallbackWarning =
                "The Application Insights telemetry channel owned by the Functions host could not be read";
            Assert.DoesNotContain(
                this.loggerProvider.GetAllLogMessages(),
                log => log.FormattedMessage?.Contains(fallbackWarning) == true);

            AssertSayHelloWithActivitySpans(result.Item1);
        }

        /*
         * Outside the Functions host there is no host configuration to forward to, so Durable still
         * has to authenticate on its own. This keeps the pre-existing behaviour covered and guards
         * the missing-spans regression that caused #3009 to be reverted by #3053.
         */
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DistributedTracingV2_EntraAuthentication_WithoutHostConfiguration_PreservesModuleSpans()
        {
            string[] orchestrationFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            TelemetryActivator activator = null;

            var result = await this.ExecuteOrchestrationWithExceptionAsync(
                orchestrationFunctionNames,
                "DTV2EntraFallback",
                "world",
                extendedSessions: false,
                protocol: "W3CTraceContext",
                version: DurableDistributedTracingVersion.V2,
                authenticationString: "Authorization=AAD",
                inspectTelemetryActivator: telemetryActivator => activator = telemetryActivator);

            Assert.NotNull(activator);
            Assert.IsNotType<HostForwardingTelemetryChannel>(activator.TelemetryConfiguration.TelemetryChannel);

            const string expectedMessage =
                "Microsoft Entra authentication enabled for Durable distributed tracing using the system-assigned managed identity.";
            Assert.Contains(
                this.loggerProvider.GetAllLogMessages(),
                log => log.FormattedMessage?.StartsWith(expectedMessage, StringComparison.Ordinal) == true);

            AssertSayHelloWithActivitySpans(result.Item1);
        }

        private static void AssertSayHelloWithActivitySpans(List<OperationTelemetry> telemetry)
        {
            DependencyTelemetry createOrchestration = Assert.Single(
                telemetry.OfType<DependencyTelemetry>(),
                item => item.Name.StartsWith($"{TraceActivityConstants.CreateOrchestration}:", StringComparison.Ordinal));
            RequestTelemetry orchestration = Assert.Single(
                telemetry.OfType<RequestTelemetry>(),
                item => item.Name.StartsWith($"{TraceActivityConstants.Orchestration}:", StringComparison.Ordinal));
            DependencyTelemetry scheduleActivity = Assert.Single(
                telemetry.OfType<DependencyTelemetry>(),
                item => item.Name.StartsWith($"{TraceActivityConstants.Activity}:", StringComparison.Ordinal));
            RequestTelemetry activity = Assert.Single(
                telemetry.OfType<RequestTelemetry>(),
                item => item.Name.StartsWith($"{TraceActivityConstants.Activity}:", StringComparison.Ordinal));

            Assert.Equal(createOrchestration.Id, orchestration.Context.Operation.ParentId);
            Assert.Equal(orchestration.Id, scheduleActivity.Context.Operation.ParentId);
            Assert.Equal(scheduleActivity.Id, activity.Context.Operation.ParentId);
            Assert.All(
                new OperationTelemetry[] { createOrchestration, orchestration, scheduleActivity, activity },
                item => Assert.Equal(orchestration.Context.Operation.Id, item.Context.Operation.Id));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, "W3CTraceContext")]
        [InlineData(true, "HttpCorrelationProtocol")]
        [InlineData(true, "W3CTraceContext")]
        [InlineData(false, "HttpCorrelationProtocol")]
        public async Task CheckCloudRoleName_RequestTelemetry_SingleOrchestration(bool extendedSessions, string protocol)
        {
            string[] orchestrationFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            var result = await
                this.ExecuteOrchestrationWithExceptionAsync(
                    orchestrationFunctionNames,
                    "SingleOrchestration",
                    "world",
                    extendedSessions,
                    protocol);

            var traceTelemetry = result.Item1;

            // Comparing cloud role name with testSiteName.toLower() to match the lowercase app name convention
            List<OperationTelemetry> requestTelemetryWithCloudRoleNamesList = traceTelemetry.Where(x => x.GetType() == typeof(RequestTelemetry) && x.Context.Cloud.RoleName.Equals(TestSiteName.ToLower())).ToList();
            Assert.NotEmpty(requestTelemetryWithCloudRoleNamesList);
        }

        /*
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, "W3CTraceContext")]
        [InlineData(true, "HttpCorrelationProtocol")]
        [InlineData(true, "W3CTraceContext")]
        [InlineData(false, "HttpCorrelationProtocol")]
        public async Task AllOrchestrationActivityActions(bool extendedSessions, string protocol)
        {
            string[] orchestrationFunctionNames =
            {
                nameof(TestOrchestrations.AllOrchestratorActivityActions),
            };

            var counterEntityId = new EntityId("Counter", Guid.NewGuid().ToString());

            var result = await
                this.ExecuteOrchestrationWithExceptionAsync(
                    orchestrationFunctionNames,
                    nameof(this.AllOrchestrationActivityActions),
                    counterEntityId,
                    extendedSessions,
                    protocol);
            var actual = result.Item1;
            Assert.Equal(15, actual.Count);
            // TODO: This part of the test is failing. This is commented out temporarily since Correlation Tracing is a WIP.
            // Assert.Single(result.Item2); // Error inside of HttpActivity since the request set to null.
            Assert.Equal(
                new (Type, string)[]
                {
                    (typeof(RequestTelemetry), $"{TraceConstants.Client}: "),  // start orchestration
                    (typeof(DependencyTelemetry), TraceConstants.Client),
                    (typeof(RequestTelemetry), $"{TraceConstants.Orchestrator} AllOrchestratorActivityActions"), // Orchestrator started
                    (typeof(DependencyTelemetry), $"{TraceConstants.Orchestrator} Hello"),
                    (typeof(RequestTelemetry), $"{TraceConstants.Activity} Hello"), // Activity Hello Started
                    (typeof(DependencyTelemetry), $"{TraceConstants.Orchestrator} Hello"),
                    (typeof(RequestTelemetry), $"{TraceConstants.Activity} Hello"),  // Activity Hello Started
                    (typeof(DependencyTelemetry), $"{TraceConstants.Orchestrator} SayHelloInline"),
                    (typeof(RequestTelemetry), $"{TraceConstants.Orchestrator} SayHelloInline"),  // SubOrchestrator SayHelloInline Started
                    (typeof(DependencyTelemetry), $"{TraceConstants.Orchestrator} SayHelloWithActivity"),
                    (typeof(RequestTelemetry), $"{TraceConstants.Orchestrator} SayHelloWithActivity"), // SubOrchestrator SayHelloWithActivity Started
                    (typeof(DependencyTelemetry), $"{TraceConstants.Orchestrator} Hello"),
                    (typeof(RequestTelemetry), $"{TraceConstants.Activity} Hello"), // Activity Hello Started by SubOrchestrator SayHelloWithActivity
                    (typeof(DependencyTelemetry), $"{TraceConstants.Orchestrator} BuiltIn::HttpActivity"),
                    (typeof(RequestTelemetry), $"{TraceConstants.Activity} BuiltIn::HttpActivity"),  // HttpActivity Started
                }.ToList(), actual.Select(x => (x.GetType(), x.Name)).ToList());
        }
        */

        internal async Task<Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>>>
            ExecuteOrchestrationWithExceptionAsync(
                string[] orchestratorFunctionNames,
                string testName,
                object input,
                bool extendedSessions,
                string protocol,
                DurableDistributedTracingVersion version = DurableDistributedTracingVersion.V1,
                string authenticationString = null,
                TelemetryConfiguration hostTelemetryConfiguration = null,
                Action<TelemetryActivator> inspectTelemetryActivator = null,
                bool captureThroughHostChannel = false)
        {
            ConcurrentQueue<ITelemetry> sendItems = new ConcurrentQueue<ITelemetry>();
            TraceOptions traceOptions = new TraceOptions()
            {
                DistributedTracingEnabled = true,
                DistributedTracingProtocol = protocol,
                Version = version,
            };
            DurableTaskOptions options = new DurableTaskOptions();
            options.Tracing = traceOptions;
            var sendAction = new Action<ITelemetry>(
                delegate(ITelemetry telemetry) { sendItems.Enqueue(telemetry); });

            // Capturing on the host channel instead of the OnSend hook leaves the TelemetryActivator
            // entirely to the production registration and proves telemetry really travels through
            // the host channel, which is what carries the host's Entra credential.
            if (captureThroughHostChannel)
            {
                hostTelemetryConfiguration.TelemetryChannel = new NoOpTelemetryChannel { OnSend = sendAction };
            }

            string siteNameEnvironmentVarName = "WEBSITE_SITE_NAME";
            string siteNameEnvironmentVarValue = TestSiteName;
            var mockNameResolver = GetNameResolverMock(
                new[]
                {
                    (siteNameEnvironmentVarName, siteNameEnvironmentVarValue),
                    ("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=00000000-0000-0000-0000-000000000000"),
                    ("APPLICATIONINSIGHTS_AUTHENTICATION_STRING", authenticationString),
                });

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                options: options,
                nameResolver: mockNameResolver.Object,
                onSend: captureThroughHostChannel ? null : sendAction,
                hostTelemetryConfiguration: hostTelemetryConfiguration))
            {
                await host.StartAsync();

                if (inspectTelemetryActivator != null)
                {
                    IServiceProvider services =
                        ((PlatformSpecificHelpers.FunctionsV2HostWrapper)host).InnerHost.Services;
                    inspectTelemetryActivator(
                        Assert.IsType<TelemetryActivator>(services.GetRequiredService<ITelemetryActivator>()));
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], input, this.output);
                await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(90));
                await host.StopAsync();
            }

            var sendItemList = this.ConvertTo(sendItems);
            var operationTelemetryList = sendItemList.OfType<OperationTelemetry>();
            var exceptionTelemetryList = sendItemList.OfType<ExceptionTelemetry>().ToList();
            var result = this.FilterOperationTelemetry(operationTelemetryList).ToList();
            return new Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>>(result.CorrelationSort(), exceptionTelemetryList);
        }

        /*
         * End to end test that checks if the DT V2 GA Announcement warning is logged
         * when distributed tracing is disabled or enabled but not Version V2.
         */
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, DurableDistributedTracingVersion.None, true, false)]
        [InlineData(true, DurableDistributedTracingVersion.None, false, false)]
        [InlineData(true, DurableDistributedTracingVersion.V1, true, false)]
        [InlineData(true, DurableDistributedTracingVersion.V2, false, false)]
        [InlineData(false, DurableDistributedTracingVersion.None, true, true)]
        [InlineData(true, DurableDistributedTracingVersion.None, false, true)]
        [InlineData(true, DurableDistributedTracingVersion.V1, true, true)]
        [InlineData(true, DurableDistributedTracingVersion.V2, false, true)]

        public void TelemetryActivator_DTV2_Announcement(bool enabled, DurableDistributedTracingVersion version, bool warningExpected, bool extendedSessions)
        {
            TraceOptions traceOptions = new TraceOptions()
            {
                DistributedTracingEnabled = enabled,
                Version = version,
            };

            DurableTaskOptions options = new DurableTaskOptions();
            options.Tracing = traceOptions;

            string connStringEnvVarName = "APPLICATIONINSIGHTS_CONNECTION_STRING";
            string connStringValue = "InstrumentationKey=xxxx;IngestionEndpoint =https://xxxx.applicationinsights.azure.com/;LiveEndpoint=https://xxxx.livediagnostics.monitor.azure.com/";

            var mockNameResolver = GetNameResolverMock(new[] { (connStringEnvVarName, connStringValue) });

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                "SingleOrchestration",
                extendedSessions,
                nameResolver: mockNameResolver.Object,
                options: options))
            {
                string tracingWarningMessage = "Durable Functions Distributed Tracing V2 is GA now! Learn how to enable the feature by visiting";
                var foundTracingWarningLog = this.loggerProvider.GetAllLogMessages().Any(l => l.FormattedMessage.StartsWith(tracingWarningMessage));

                Assert.Equal(warningExpected, foundTracingWarningLog);
            }
        }

        /*
        * End to end test that checks if a warning is logged when distributed tracing is
        * enabled, but APPINSIGHTS_INSTRUMENTATIONKEY isn't set. The test also checks
        * that the warning isn't logged when the environment variable is set.
        */
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void TelemetryClientSetup_AppInsights_Warnings(bool instrumentationKeyIsSet, bool connStringIsSet, bool extendedSessions)
        {
            TraceOptions traceOptions = new TraceOptions()
            {
                DistributedTracingEnabled = true,
                DistributedTracingProtocol = "W3CTraceContext",
            };

            DurableTaskOptions options = new DurableTaskOptions();
            options.Tracing = traceOptions;

            string instKeyEnvVarName = "APPINSIGHTS_INSTRUMENTATIONKEY";
            string connStringEnvVarName = "APPLICATIONINSIGHTS_CONNECTION_STRING";
            string environmentVariableValue = "test value";
            string connStringValue = "InstrumentationKey=xxxx;IngestionEndpoint =https://xxxx.applicationinsights.azure.com/;LiveEndpoint=https://xxxx.livediagnostics.monitor.azure.com/";

            var mockNameResolver = GetNameResolverMock(new[] { (instKeyEnvVarName, string.Empty), (connStringEnvVarName, string.Empty) });

            if (instrumentationKeyIsSet && connStringIsSet)
            {
                mockNameResolver = GetNameResolverMock(new[] { (instKeyEnvVarName, environmentVariableValue), (connStringEnvVarName, connStringValue) });
            }
            else if (instrumentationKeyIsSet)
            {
                mockNameResolver = GetNameResolverMock(new[] { (instKeyEnvVarName, environmentVariableValue), (connStringEnvVarName, string.Empty) });
            }
            else if (connStringIsSet)
            {
                mockNameResolver = GetNameResolverMock(new[] { (instKeyEnvVarName, string.Empty), (connStringEnvVarName, connStringValue) });
            }

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                "SingleOrchestration",
                extendedSessions,
                nameResolver: mockNameResolver.Object,
                options: options))
            {
                string bothSettingsSetWarningMessage = "Both 'APPINSIGHTS_INSTRUMENTATIONKEY' and 'APPLICATIONINSIGHTS_CONNECTION_STRING' are defined in the current environment variables. Please specify one. We recommend specifying 'APPLICATIONINSIGHTS_CONNECTION_STRING'.";
                var bothSettingsSetWarningLogMessage = this.loggerProvider.GetAllLogMessages().Where(l => l.FormattedMessage.StartsWith(bothSettingsSetWarningMessage));

                string neitherSettingsSetWarningMessage = "'APPINSIGHTS_INSTRUMENTATIONKEY' or 'APPLICATIONINSIGHTS_CONNECTION_STRING' were not defined in the current environment variables, but distributed tracing is enabled. Please specify one. We recommend specifying 'APPLICATIONINSIGHTS_CONNECTION_STRING'.";
                var neitherSettingsSetWarningLogMessage = this.loggerProvider.GetAllLogMessages().Where(l => l.FormattedMessage.StartsWith(neitherSettingsSetWarningMessage));

                string settingUpTelemetryClientMessage = "Setting up the telemetry client...";
                var settingUpTelemetryClientLogMessage = this.loggerProvider.GetAllLogMessages().Where(l => l.FormattedMessage.StartsWith(settingUpTelemetryClientMessage));

                string readingInstrumentationKeyMessage = "Reading APPINSIGHTS_INSTRUMENTATIONKEY...";
                var readingInstrumentationKeyLogMessage = this.loggerProvider.GetAllLogMessages().Where(l => l.FormattedMessage.StartsWith(readingInstrumentationKeyMessage));

                string readingConnStringMessage = "Reading APPLICATIONINSIGHTS_CONNECTION_STRING...";
                var readingConnStringLogMessage = this.loggerProvider.GetAllLogMessages().Where(l => l.FormattedMessage.StartsWith(readingConnStringMessage));

                if (instrumentationKeyIsSet && connStringIsSet)
                {
                    Assert.Single(bothSettingsSetWarningLogMessage);
                    Assert.Empty(neitherSettingsSetWarningLogMessage);
                    Assert.Single(settingUpTelemetryClientLogMessage);
                    Assert.Single(readingInstrumentationKeyLogMessage);
                    Assert.Single(readingConnStringLogMessage);
                }
                else if (instrumentationKeyIsSet && !connStringIsSet)
                {
                    Assert.Empty(bothSettingsSetWarningLogMessage);
                    Assert.Empty(neitherSettingsSetWarningLogMessage);
                    Assert.Single(settingUpTelemetryClientLogMessage);
                    Assert.Single(readingInstrumentationKeyLogMessage);
                    Assert.Empty(readingConnStringLogMessage);
                }
                else if (!instrumentationKeyIsSet && connStringIsSet)
                {
                    Assert.Empty(bothSettingsSetWarningLogMessage);
                    Assert.Empty(neitherSettingsSetWarningLogMessage);
                    Assert.Single(settingUpTelemetryClientLogMessage);
                    Assert.Empty(readingInstrumentationKeyLogMessage);
                    Assert.Single(readingConnStringLogMessage);
                }
                else
                {
                    Assert.Empty(bothSettingsSetWarningLogMessage);
                    Assert.Single(neitherSettingsSetWarningLogMessage);
                    Assert.Single(settingUpTelemetryClientLogMessage);
                    Assert.Empty(readingInstrumentationKeyLogMessage);
                    Assert.Empty(readingConnStringLogMessage);
                }
            }
        }

        private static Mock<INameResolver> GetNameResolverMock((string Key, string Value)[] settings)
        {
            var mock = new Mock<INameResolver>();
            foreach (var setting in settings)
            {
                mock.Setup(x => x.Resolve(setting.Key)).Returns(setting.Value);
            }

            return mock;
        }

        private IEnumerable<OperationTelemetry> FilterOperationTelemetry(IEnumerable<OperationTelemetry> operationTelemetries)
        {
            return operationTelemetries.Where(
                p => p.Name.Contains(TraceConstants.Activity) ||
                    p.Name.Contains(TraceConstants.Orchestrator) ||
                    p.Name.Contains(TraceConstants.Client) ||
                    p.Name.Contains("Operation") ||
                    p.Name.StartsWith($"{TraceActivityConstants.CreateOrchestration}:", StringComparison.Ordinal) ||
                    p.Name.StartsWith($"{TraceActivityConstants.Orchestration}:", StringComparison.Ordinal) ||
                    p.Name.StartsWith($"{TraceActivityConstants.Activity}:", StringComparison.Ordinal));
        }

        private List<ITelemetry> ConvertTo(ConcurrentQueue<ITelemetry> queue)
        {
            var converted = new List<ITelemetry>();
            while (!queue.IsEmpty)
            {
                ITelemetry x;
                if (queue.TryDequeue(out x))
                {
                    converted.Add(x);
                }
            }

            return converted;
        }

        private static void AssertRawV2Topology(OperationTelemetry[] spans)
        {
            var scenarios = new[]
            {
                (nameof(TestOrchestrations.SayHelloWithActivity), new[] { "activity:Hello" }),
                (nameof(TestOrchestrations.ThrowOrchestrator), new[] { "activity:ThrowActivity" }),
                (nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore), new[] { "entity:stringstore2:set", "entity:stringstore2:delete" }),
            };
            foreach (var (orchestrationName, childNames) in scenarios)
            {
                RequestTelemetry orchestration = Assert.Single(
                    spans.OfType<RequestTelemetry>(), span => span.Name == $"orchestration:{orchestrationName}");
                DependencyTelemetry create = Assert.Single(
                    spans.OfType<DependencyTelemetry>(), span => span.Name == $"create_orchestration:{orchestrationName}");
                RequestTelemetry start = Assert.Single(
                    spans.OfType<RequestTelemetry>(), span => span.Name == "StartFunction" &&
                        span.Context.Operation.Id == orchestration.Context.Operation.Id);
                Assert.True(string.IsNullOrEmpty(start.Context.Operation.ParentId));
                AssertSpanParent(create, start);
                AssertSpanParent(orchestration, create);
                foreach (string childName in childNames)
                {
                    DependencyTelemetry schedule = Assert.Single(
                        spans.OfType<DependencyTelemetry>(), span => span.Name == childName);
                    RequestTelemetry execution = Assert.Single(
                        spans.OfType<RequestTelemetry>(), span => span.Name == childName);
                    AssertSpanParent(schedule, orchestration);
                    AssertSpanParent(execution, schedule);
                }
            }
        }

        private static void AssertSpanParent(OperationTelemetry child, OperationTelemetry parent)
        {
            Assert.False(string.IsNullOrEmpty(child.Context.Operation.ParentId));
            Assert.Equal(parent.Id, child.Context.Operation.ParentId);
            Assert.Equal(parent.Context.Operation.Id, child.Context.Operation.Id);
        }

        private static object CaptureRawSpan(OperationTelemetry telemetry)
        {
            var request = telemetry as RequestTelemetry;
            var dependency = telemetry as DependencyTelemetry;
            return new
            {
                TelemetryType = telemetry.GetType(),
                telemetry.Id,
                OperationId = telemetry.Context.Operation.Id,
                ParentId = telemetry.Context.Operation.ParentId,
                telemetry.Timestamp,
                telemetry.Duration,
                telemetry.Name,
                telemetry.Success,
                ResponseCode = request?.ResponseCode,
                RequestUrl = request?.Url?.OriginalString,
                RequestSource = request?.Source,
                ResultCode = dependency?.ResultCode,
                DependencyType = dependency?.Type,
                DependencyTarget = dependency?.Target,
                DependencyData = dependency?.Data,
            };
        }

        private sealed class HostMetadataInitializer : ITelemetryInitializer
        {
            public ConcurrentDictionary<OperationTelemetry, object> BeforeEnrichment { get; } =
                new ConcurrentDictionary<OperationTelemetry, object>(ReferenceEqualityComparer.Instance);

            public void Initialize(ITelemetry telemetry)
            {
                if (telemetry is OperationTelemetry span)
                {
                    // The host SDK can initialize a request at both start and completion.
                    // Preserve the latest pre-enrichment values, when producer fields are final.
                    this.BeforeEnrichment[span] = CaptureRawSpan(span);
                }

                telemetry.Context.Cloud.RoleName = "orders-functions";
                if (telemetry is ISupportProperties properties)
                {
                    properties.Properties["host-enrichment"] = "enabled";
                }
            }
        }
    }

#pragma warning disable SA1402
    public static class ListExtensions
    {
        public static List<OperationTelemetry> CorrelationSort(this List<OperationTelemetry> telemetries)
        {
            var result = new List<OperationTelemetry>();
            if (telemetries.Count == 0)
            {
                return result;
            }

            // Sort by the timestamp
            var sortedTelemetries = telemetries.OrderBy(p => p.Timestamp.Ticks).ToList();

            // pick the first one as the parent. remove it from the list.
            var parent = sortedTelemetries.First();
            result.Add(parent);
            sortedTelemetries.RemoveOperationTelemetry(parent);

            // find the child recursively and remove the child and pass it as a parameter
            var sortedList = GetCorrelationSortedList(parent, sortedTelemetries);
            result.AddRange(sortedList);
            return result;
        }

        public static bool RemoveOperationTelemetry(this List<OperationTelemetry> telemetries, OperationTelemetry telemetry)
        {
            int index = -1;
            for (var i = 0; i < telemetries.Count; i++)
            {
                if (telemetries[i].Id == telemetry.Id)
                {
                    index = i;
                }
            }

            if (index == -1)
            {
                return false;
            }

            telemetries.RemoveAt(index);
            return true;
        }

        private static List<OperationTelemetry> GetCorrelationSortedList(OperationTelemetry parent, List<OperationTelemetry> current)
        {
            var result = new List<OperationTelemetry>();
            if (current.Count != 0)
            {
                IOrderedEnumerable<OperationTelemetry> nexts = current.Where(p => p.Context.Operation.ParentId == parent.Id).OrderBy(p => p.Timestamp.Ticks);
                foreach (OperationTelemetry next in nexts)
                {
                    current.RemoveOperationTelemetry(next);
                    result.Add(next);
                    var childResult = GetCorrelationSortedList(next, current);
                    result.AddRange(childResult);
                }
            }

            return result;
        }
    }

    [CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
    public class NonParallelCollectionDefinitionClass
    {
    }
#pragma warning restore SA1402
}
