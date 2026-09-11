// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Settings;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Collection("Non-Parallel Collection")]
    public class HostTelemetryInitializerTests : IDisposable
    {
        private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
        private const string InvocationInitializerName = "Microsoft.Azure.WebJobs.Logging.ApplicationInsights.WebJobsTelemetryInitializer";
        private static readonly Lazy<Assembly> IsolatedHostTestAssembly = new Lazy<Assembly>(LoadIsolatedHostTestAssembly);
        private readonly bool originalDistributedTracing = CorrelationSettings.Current.EnableDistributedTracing;
        private readonly Protocol originalProtocol = CorrelationSettings.Current.Protocol;
        private readonly ActivityIdFormat originalIdFormat = Activity.DefaultIdFormat;
        private readonly bool originalForceIdFormat = Activity.ForceDefaultIdFormat;
        private readonly Activity originalActivity = Activity.Current;
        private readonly ITestOutputHelper output;

        public HostTelemetryInitializerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public void TraceOptions_ReportsOptInWithoutChangingExistingDebugFields(bool enabled)
        {
            var options = new TraceOptions();
            Assert.False(options.UseHostTelemetryInitializers);
            options.UseHostTelemetryInitializers = enabled;
            var debug = new StringBuilder();

            options.AddToDebugString(debug);

            Assert.Equal(
                $"TraceReplayEvents: False, TraceInputsAndOutputs: False, UseHostTelemetryInitializers: {enabled}",
                debug.ToString());
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public void Initialize_BindsOptInThroughRegisteredDurableTaskOptions(bool? enabled)
        {
            const string tracingPath = "AzureWebJobs:extensions:durableTask:tracing:";
            var configuration = new Dictionary<string, string>
            {
                [tracingPath + "distributedTracingEnabled"] = "true",
                [tracingPath + "version"] = "V2",
            };
            if (enabled.HasValue)
            {
                configuration[tracingPath + "useHostTelemetryInitializers"] = enabled.Value.ToString();
            }

            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureWebJobs(builder => builder.AddDurableTask())
                .Build();
            var options = host.Services.GetRequiredService<IOptions<DurableTaskOptions>>().Value;
            Assert.True(options.Tracing.DistributedTracingEnabled);
            Assert.Equal(DurableDistributedTracingVersion.V2, options.Tracing.Version);
            Assert.Equal(enabled == true, options.Tracing.UseHostTelemetryInitializers);

            using var hostConfiguration = CreateHostConfiguration();
            hostConfiguration.TelemetryInitializers.Add(new CallbackInitializer(item => item.Context.Cloud.RoleName = "bound-host-role"));
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add, options);

            activator.Initialize(NullLogger.Instance);
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            Assert.Equal(enabled == true ? "bound-host-role" : null, Assert.Single(captured).Context.Cloud.RoleName);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null, true, DurableDistributedTracingVersion.V2)]
        [InlineData(false, true, DurableDistributedTracingVersion.V2)]
        [InlineData(true, true, DurableDistributedTracingVersion.V2)]
        [InlineData(null, true, DurableDistributedTracingVersion.V1)]
        [InlineData(false, true, DurableDistributedTracingVersion.V1)]
        [InlineData(true, true, DurableDistributedTracingVersion.V1)]
        [InlineData(null, true, DurableDistributedTracingVersion.None)]
        [InlineData(false, true, DurableDistributedTracingVersion.None)]
        [InlineData(true, true, DurableDistributedTracingVersion.None)]
        [InlineData(null, false, DurableDistributedTracingVersion.V2)]
        [InlineData(false, false, DurableDistributedTracingVersion.V2)]
        [InlineData(true, false, DurableDistributedTracingVersion.V2)]
        [InlineData(true, false, DurableDistributedTracingVersion.V1)]
        [InlineData(true, false, DurableDistributedTracingVersion.None)]
        public void Initialize_ImportsOnlyForEnabledV2OptIn(
            bool? useHostInitializers,
            bool distributedTracingEnabled,
            DurableDistributedTracingVersion version)
        {
            using var hostConfiguration = CreateHostConfiguration();
            var initializer = new CallbackInitializer(item => item.Context.Cloud.RoleName = "orders-functions");
            hostConfiguration.TelemetryInitializers.Add(initializer);
            var options = new DurableTaskOptions();
            options.Tracing.DistributedTracingEnabled = distributedTracingEnabled;
            options.Tracing.Version = version;
            if (useHostInitializers.HasValue)
            {
                options.Tracing.UseHostTelemetryInitializers = useHostInitializers.Value;
            }

            var logger = new TestLogger(this.output, "UnitTest");
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add, options);

            activator.Initialize(logger);

            bool usesV2 = distributedTracingEnabled && version == DurableDistributedTracingVersion.V2;
            bool imports = useHostInitializers == true && usesV2;
            var warnings = logger.LogMessages.Where(
                message => message.Level == LogLevel.Warning &&
                    message.FormattedMessage.Contains("UseHostTelemetryInitializers")).ToArray();
            if (useHostInitializers == true && !usesV2)
            {
                Assert.Contains(
                    "UseHostTelemetryInitializers requires distributed tracing to be enabled with version V2",
                    Assert.Single(warnings).FormattedMessage);
            }
            else
            {
                Assert.Empty(warnings);
            }

            Assert.Equal(usesV2, activator.TelemetryModule != null);
            Assert.Equal(usesV2, activator.WebJobsTelemetryModule != null);
            if (!distributedTracingEnabled || version == DurableDistributedTracingVersion.None)
            {
                Assert.Null(activator.TelemetryConfiguration);
                Assert.Empty(captured);
                return;
            }

            Assert.Equal(imports, activator.TelemetryConfiguration.TelemetryInitializers.Contains(initializer));
            Assert.IsType<OperationCorrelationTelemetryInitializer>(activator.TelemetryConfiguration.TelemetryInitializers.First());
            Assert.IsType<DurableTaskInstanceIdTelemetryInitializer>(activator.TelemetryConfiguration.TelemetryInitializers.Last());
            Assert.Equal(imports ? 3 : 2, activator.TelemetryConfiguration.TelemetryInitializers.Count);
            Assert.Equal(imports ? 1 : 0, logger.LogMessages.Count(
                message => message.FormattedMessage.Contains("Imported:")));
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            var request = Assert.IsType<RequestTelemetry>(Assert.Single(captured));
            Assert.Equal(imports ? "orders-functions" : null, request.Context.Cloud.RoleName);
            Assert.Equal("orchestration:Order", request.Name);
            Assert.Equal("orchestration:Order", request.Context.Operation.Name);
            Assert.False(request.Success);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public void Initialize_MissingHostKeepsDurableTracingAndWarnsOnlyWhenRequested(bool enabled)
        {
            var options = V2Options();
            options.Tracing.UseHostTelemetryInitializers = enabled;
            var logger = new TestLogger(this.output, "UnitTest");
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(null, captured.Add, options);

            activator.Initialize(logger);
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            var warnings = logger.LogMessages.Where(message => message.Level == LogLevel.Warning).ToArray();
            if (enabled)
            {
                Assert.Contains(
                    "UseHostTelemetryInitializers is enabled, but the Functions host telemetry configuration is unavailable",
                    Assert.Single(warnings).FormattedMessage);
            }
            else
            {
                Assert.Empty(warnings);
            }

            Assert.NotNull(activator.TelemetryModule);
            Assert.NotNull(activator.WebJobsTelemetryModule);
            var request = Assert.IsType<RequestTelemetry>(Assert.Single(captured));
            Assert.Equal("orchestration:Order", request.Context.Operation.Name);
            Assert.Null(request.Context.Cloud.RoleName);
            Assert.False(request.Properties.ContainsKey(LogConstants.InvocationIdKey));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void Initialize_NoEligibleHostEntriesReportsZeroAndStillEmits(bool includeCorrelationDefault, int excluded)
        {
            using var hostConfiguration = CreateHostConfiguration();
            if (!includeCorrelationDefault)
            {
                hostConfiguration.TelemetryInitializers.Clear();
            }

            var logger = new TestLogger(this.output, "UnitTest");
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add);

            activator.Initialize(logger);
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            var summary = Assert.Single(logger.LogMessages, message => message.FormattedMessage.Contains("Imported:"));
            Assert.Equal(LogLevel.Information, summary.Level);
            Assert.Contains($"Imported: 0. Excluded: {excluded}.", summary.FormattedMessage);
            Assert.DoesNotContain(logger.LogMessages, message => message.Level == LogLevel.Warning);
            Assert.Equal(2, activator.TelemetryConfiguration.TelemetryInitializers.Count);
            Assert.Null(Assert.Single(captured).Context.Cloud.RoleName);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_PreservesHostOrderSameTypeInstancesAndRepeatedRegistrations()
        {
            using var hostConfiguration = CreateHostConfiguration();
            var calls = new List<string>();
            var first = new CallbackInitializer(item =>
            {
                calls.Add("first");
                item.Context.Cloud.RoleName = "first-role";
            });
            var second = new CallbackInitializer(item =>
            {
                Assert.Equal("first-role", item.Context.Cloud.RoleName);
                calls.Add("second");
                item.Context.Cloud.RoleName = "second-role";
            });
            hostConfiguration.TelemetryInitializers.Add(first);
            hostConfiguration.TelemetryInitializers.Add(second);
            hostConfiguration.TelemetryInitializers.Add(first);
            var hostSnapshot = hostConfiguration.TelemetryInitializers.ToArray();
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add);

            activator.Initialize(NullLogger.Instance);

            Assert.Empty(calls);
            Assert.Equal<ITelemetryInitializer>(
                new ITelemetryInitializer[] { first, second, first },
                activator.TelemetryConfiguration.TelemetryInitializers.Skip(1).Take(3));
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            Assert.Equal(new[] { "first", "second", "first" }, calls);
            Assert.Equal("first-role", Assert.Single(captured).Context.Cloud.RoleName);
            Assert.Equal<ITelemetryInitializer>(hostSnapshot, hostConfiguration.TelemetryInitializers);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_UsesExactTypeNamesAndKeepsCustomSubclasses()
        {
            using var hostConfiguration = CreateHostConfiguration();
            var hostDefault = hostConfiguration.TelemetryInitializers.Single();
            var sameShortName = new WebJobsTelemetryInitializer();
            var sameFullName = new global::Microsoft.Azure.WebJobs.Logging.ApplicationInsights.WebJobsTelemetryInitializer();
            var derivedCorrelation = new CustomOperationCorrelationInitializer();
            var accessor = Mock.Of<IHttpContextAccessor>();
            var excludedClientIp = new ClientIpHeaderTelemetryInitializer(accessor);
            var derivedClientIp = new CustomClientIpInitializer(accessor);
            ITelemetryInitializer[] eligible = { sameShortName, sameFullName, derivedCorrelation, derivedClientIp };
            hostConfiguration.TelemetryInitializers.Add(excludedClientIp);
            foreach (var initializer in eligible)
            {
                hostConfiguration.TelemetryInitializers.Add(initializer);
            }

            using var activator = CreateActivator(hostConfiguration);
            activator.Initialize(NullLogger.Instance);

            Assert.Equal(InvocationInitializerName, sameFullName.GetType().FullName);
            Assert.NotEqual(typeof(ApplicationInsightsLoggerOptions).Assembly, sameFullName.GetType().Assembly);
            var privateInitializers = activator.TelemetryConfiguration.TelemetryInitializers;
            var privateDefault = Assert.Single(privateInitializers, initializer => initializer.GetType() == typeof(OperationCorrelationTelemetryInitializer));
            Assert.Same(privateDefault, privateInitializers.First());
            Assert.NotSame(hostDefault, privateDefault);
            Assert.DoesNotContain(excludedClientIp, privateInitializers);
            Assert.Equal<ITelemetryInitializer>(eligible, privateInitializers.Skip(1).Take(eligible.Length));
            Assert.IsType<DurableTaskInstanceIdTelemetryInitializer>(privateInitializers.Last());
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public void Initialize_ExcludesAmbientInitializersAcrossLoadContexts(bool clientIp)
        {
            var custom = new CallbackInitializer(item => item.Context.Cloud.RoleName = "host-custom");
            using var host = BuildWebJobsHost(
                new HttpContextAccessor(),
                services => services.AddSingleton<ITelemetryInitializer>(custom),
                useSeparateLoadContext: true);
            var hostConfiguration = host.Services.GetRequiredService<TelemetryConfiguration>();
            var marker = clientIp ? typeof(ClientIpHeaderTelemetryInitializer) : typeof(ApplicationInsightsLoggerOptions);
            string fullName = clientIp ? marker.FullName : InvocationInitializerName;
            var excluded = Assert.Single(hostConfiguration.TelemetryInitializers, initializer => initializer.GetType().FullName == fullName);
            var hostAssembly = excluded.GetType().Assembly;
            Assert.Equal(marker.Assembly.FullName, hostAssembly.FullName);
            Assert.NotSame(marker.Assembly, hostAssembly);
            Assert.NotSame(AssemblyLoadContext.GetLoadContext(marker.Assembly), AssemblyLoadContext.GetLoadContext(hostAssembly));
            Assert.Contains(typeof(ITelemetryInitializer), excluded.GetType().GetInterfaces());
            using var activator = CreateActivator(hostConfiguration);

            activator.Initialize(NullLogger.Instance);

            Assert.DoesNotContain(excluded, activator.TelemetryConfiguration.TelemetryInitializers);
            Assert.Contains(custom, activator.TelemetryConfiguration.TelemetryInitializers);
            Assert.Contains(excluded, hostConfiguration.TelemetryInitializers);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, true, true)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        public void Initialize_MatchesAssemblyNameAndPublicKeyTokenRegardlessOfVersion(
            bool clientIp,
            bool sameAssemblyName,
            bool samePublicKey)
        {
            var marker = clientIp ? typeof(ClientIpHeaderTelemetryInitializer) : typeof(ApplicationInsightsLoggerOptions);
            var markerIdentity = marker.Assembly.GetName();
            var identity = new AssemblyName
            {
                Name = sameAssemblyName ? markerIdentity.Name : "CustomInitializers",
                Version = new Version(markerIdentity.Version.Major + 1, 0, 0, 0),
            };
            if (samePublicKey)
            {
                identity.SetPublicKey(markerIdentity.GetPublicKey());
            }

            string fullName = clientIp ? marker.FullName : InvocationInitializerName;
            var initializer = CreateInitializerLookalike(fullName, identity);
            Assert.NotEqual(markerIdentity.Version, initializer.GetType().Assembly.GetName().Version);
            Assert.NotEmpty(markerIdentity.GetPublicKeyToken());
            Assert.Equal(samePublicKey, markerIdentity.GetPublicKeyToken().SequenceEqual(initializer.GetType().Assembly.GetName().GetPublicKeyToken()));
            using var hostConfiguration = CreateHostConfiguration();
            hostConfiguration.TelemetryInitializers.Add(initializer);
            using var activator = CreateActivator(hostConfiguration);

            activator.Initialize(NullLogger.Instance);

            Assert.Equal(
                !(sameAssemblyName && samePublicKey),
                activator.TelemetryConfiguration.TelemetryInitializers.Contains(initializer));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public void Initialize_CharacterizesWebJobs345InitializerInventory(bool registerHttpContextAccessor)
        {
            IHttpContextAccessor accessor = registerHttpContextAccessor ? new HttpContextAccessor() : null;
            var finalInitializer = new CallbackInitializer(item => item.Context.GlobalProperties["final-host-configuration"] = "present");
            using var host = BuildWebJobsHost(
                accessor,
                additionalConfiguration: configuration => configuration.TelemetryInitializers.Add(finalInitializer));
            var hostConfiguration = host.Services.GetRequiredService<TelemetryConfiguration>();
            var snapshot = hostConfiguration.TelemetryInitializers.ToArray();
            var expectedNames = new List<string>
            {
                typeof(OperationCorrelationTelemetryInitializer).FullName,
                "Microsoft.ApplicationInsights.DependencyCollector.HttpDependenciesParsingTelemetryInitializer",
            };
            if (registerHttpContextAccessor)
            {
                expectedNames.Add(typeof(ClientIpHeaderTelemetryInitializer).FullName);
            }

            expectedNames.Add("Microsoft.Azure.WebJobs.Logging.ApplicationInsights.WebJobsRoleEnvironmentTelemetryInitializer");
            expectedNames.Add(InvocationInitializerName);
            expectedNames.Add("Microsoft.Azure.WebJobs.Logging.ApplicationInsights.MetricSdkVersionTelemetryInitializer");
            expectedNames.Add(typeof(CallbackInitializer).FullName);

            // This pins the bare WebJobs inventory, not the additional Functions Script host registrations.
            Assert.StartsWith("3.0.45", FileVersionInfo.GetVersionInfo(typeof(ApplicationInsightsLoggerOptions).Assembly.Location).ProductVersion);
            Assert.Equal(expectedNames, snapshot.Select(initializer => initializer.GetType().FullName));
            var invocationInitializer = Assert.Single(snapshot, initializer => initializer.GetType().FullName == InvocationInitializerName);
            Assert.Same(typeof(ApplicationInsightsLoggerOptions).Assembly, invocationInitializer.GetType().Assembly);
            using var activator = CreateActivator(hostConfiguration);
            var logger = new TestLogger(this.output, "UnitTest");

            activator.Initialize(logger);

            var imported = activator.TelemetryConfiguration.TelemetryInitializers;
            Assert.DoesNotContain(invocationInitializer, imported);
            Assert.DoesNotContain(imported, initializer => initializer.GetType() == typeof(ClientIpHeaderTelemetryInitializer));
            Assert.Equal<ITelemetryInitializer>(
                snapshot.Where(initializer =>
                    initializer != invocationInitializer &&
                    initializer.GetType() != typeof(OperationCorrelationTelemetryInitializer) &&
                    initializer.GetType() != typeof(ClientIpHeaderTelemetryInitializer)),
                imported.Skip(1).Take(imported.Count - 2));
            Assert.Same(finalInitializer, imported[imported.Count - 2]);
            Assert.Equal<ITelemetryInitializer>(snapshot, hostConfiguration.TelemetryInitializers);
            var summary = Assert.Single(logger.LogMessages, message => message.FormattedMessage.Contains("Imported:"));
            Assert.Equal(LogLevel.Information, summary.Level);
            Assert.Contains($"Imported: {imported.Count - 2}. Excluded: {(registerHttpContextAccessor ? 3 : 2)}.", summary.FormattedMessage);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public void Track_SupportedHostEnrichmentPreservesProducerFieldsUnderUnrelatedAmbientContext(bool useSeparateLoadContext, bool tagActivity)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.42";
            httpContext.Features.Set(new RequestTelemetry());
            var accessor = Mock.Of<IHttpContextAccessor>(value => value.HttpContext == httpContext);
            var enricher = new CallbackInitializer(item =>
            {
                item.Context.Cloud.RoleName = "orders-functions";
                item.Context.GlobalProperties["environment"] = "production";
                item.Context.GlobalProperties["serviceOwner"] = "payments-team";
            });
            using var host = BuildWebJobsHost(
                accessor,
                services => services.AddSingleton<ITelemetryInitializer>(enricher),
                useSeparateLoadContext: useSeparateLoadContext);
            var hostConfiguration = host.Services.GetRequiredService<TelemetryConfiguration>();
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add);
            activator.Initialize(NullLogger.Instance);
            var client = new TelemetryClient(activator.TelemetryConfiguration);
            var request = NewRequest();
            var dependency = new DependencyTelemetry
            {
                Name = "activity:Charge",
                Type = "DurableTask",
                Target = "Charge",
                Id = "2222222222222222",
                Timestamp = request.Timestamp,
                Duration = request.Duration,
                ResultCode = "Failed",
                Success = false,
            };
            dependency.Context.Operation.Id = request.Context.Operation.Id;
            dependency.Context.Operation.ParentId = request.Id;
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Function.UnrelatedHostInvocation");
            using var scope = logger.BeginScope(new Dictionary<string, object>
            {
                [ScopeKeys.FunctionName] = "UnrelatedHostInvocation",
                [ScopeKeys.FunctionInvocationId] = "unrelated-invocation",
                [LogConstants.CategoryNameKey] = "Function.UnrelatedHostInvocation",
            });
            using var ambient = new Activity("unrelated-http")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            if (tagActivity)
            {
                ambient.SetTag(LogConstants.NameKey, "UnrelatedHostInvocation");
                ambient.SetTag(LogConstants.SucceededKey, "true");
            }

            // Prove the actual excluded registrations would contaminate this same test context.
            var unsafeRequest = NewRequest();
            hostConfiguration.TelemetryInitializers.Single(initializer => initializer.GetType().FullName == InvocationInitializerName)
                .Initialize(unsafeRequest);
            Assert.Equal("UnrelatedHostInvocation", unsafeRequest.Context.Operation.Name);
            Assert.Equal("unrelated-invocation", unsafeRequest.Properties[LogConstants.InvocationIdKey]);
            if (tagActivity)
            {
                Assert.Equal("UnrelatedHostInvocation", unsafeRequest.Name);
                Assert.True(unsafeRequest.Success);
            }

            var unsafeDependency = new DependencyTelemetry();
            hostConfiguration.TelemetryInitializers.Single(initializer => initializer.GetType().FullName == typeof(ClientIpHeaderTelemetryInitializer).FullName)
                .Initialize(unsafeDependency);
            Assert.Equal("203.0.113.42", unsafeDependency.Context.Location.Ip);

            client.TrackRequest(request);
            client.TrackDependency(dependency);

            Assert.Equal(2, captured.Count);
            Assert.Same(request, captured[0]);
            Assert.Same(dependency, captured[1]);
            Assert.Equal("orchestration:Order", request.Name);
            Assert.Equal("orchestration:Order", request.Context.Operation.Name);
            Assert.Equal("1111111111111111", request.Id);
            Assert.Equal("0123456789abcdef0123456789abcdef", request.Context.Operation.Id);
            Assert.Equal("abcdefabcdefabcd", request.Context.Operation.ParentId);
            Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), request.Timestamp);
            Assert.Equal(TimeSpan.FromMilliseconds(123), request.Duration);
            Assert.Equal("500", request.ResponseCode);
            Assert.False(request.Success);
            Assert.Equal("activity:Charge", dependency.Name);
            Assert.Equal("activity:Charge", dependency.Context.Operation.Name);
            Assert.Equal("DurableTask", dependency.Type);
            Assert.Equal("Charge", dependency.Target);
            Assert.Equal("2222222222222222", dependency.Id);
            Assert.Equal(request.Context.Operation.Id, dependency.Context.Operation.Id);
            Assert.Equal(request.Id, dependency.Context.Operation.ParentId);
            Assert.Equal(request.Timestamp, dependency.Timestamp);
            Assert.Equal(request.Duration, dependency.Duration);
            Assert.Equal("Failed", dependency.ResultCode);
            Assert.False(dependency.Success);
            Assert.All(captured, item =>
            {
                Assert.Equal("orders-functions", item.Context.Cloud.RoleName);
                Assert.Equal("production", item.Context.GlobalProperties["environment"]);
                Assert.Equal("payments-team", item.Context.GlobalProperties["serviceOwner"]);
                Assert.Null(item.Context.Location.Ip);
                var properties = Assert.IsAssignableFrom<ISupportProperties>(item).Properties;
                Assert.False(properties.ContainsKey(LogConstants.InvocationIdKey));
                Assert.False(properties.ContainsKey(LogConstants.CategoryNameKey));
            });
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("Orders", "production", null, "orders")]
        [InlineData("Orders", "staging", null, "orders-staging")]
        [InlineData("Orders", "staging", "ExplicitRole", "ExplicitRole")]
        [InlineData(null, null, null, null)]
        public void Track_RealHostEnvironmentInitializerRetainsSiteSlotAndExplicitRole(
            string site,
            string slot,
            string roleOverride,
            string expectedRole)
        {
            string[] variables = { "WEBSITE_SITE_NAME", "WEBSITE_SLOT_NAME", "WEBSITE_CLOUD_ROLENAME" };
            string[] originalValues = variables.Select(Environment.GetEnvironmentVariable).ToArray();
            try
            {
                Environment.SetEnvironmentVariable(variables[0], site);
                Environment.SetEnvironmentVariable(variables[1], slot);
                Environment.SetEnvironmentVariable(variables[2], roleOverride);
                using var host = BuildWebJobsHost();
                var hostConfiguration = host.Services.GetRequiredService<TelemetryConfiguration>();
                var captured = new List<ITelemetry>();
                using var activator = CreateActivator(hostConfiguration, captured.Add);
                activator.Initialize(NullLogger.Instance);
                var client = new TelemetryClient(activator.TelemetryConfiguration);

                client.TrackRequest(NewRequest());

                Assert.Equal(expectedRole, Assert.Single(captured).Context.Cloud.RoleName);
                Environment.SetEnvironmentVariable("WEBSITE_CLOUD_ROLENAME", "updated-role");
                client.TrackRequest(NewRequest());
                Assert.Equal(2, captured.Count);
                Assert.Equal("updated-role", captured[1].Context.Cloud.RoleName);
            }
            finally
            {
                for (int index = 0; index < variables.Length; index++)
                {
                    Environment.SetEnvironmentVariable(variables[index], originalValues[index]);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, "orchestration", null, "custom-order", "custom-order")]
        [InlineData(true, "orchestration", null, "custom-order", "custom-order (order-123)")]
        [InlineData(true, "activity", null, "custom-order", "custom-order (order-123)")]
        [InlineData(true, "entity", null, "custom-order", "custom-order (order-123)")]
        [InlineData(true, "orchestration", "create_orchestration", "custom-order", "custom-order")]
        [InlineData(true, null, null, "custom-order", "custom-order")]
        [InlineData(false, null, null, null, "orchestration:Order")]
        [InlineData(true, "orchestration", null, null, "orchestration:Order (order-123)")]
        public void Track_CustomNamingRunsBeforeExistingInstanceIdRules(
            bool includeInstanceId,
            string taskType,
            string operation,
            string customName,
            string expectedName)
        {
            using var hostConfiguration = CreateHostConfiguration();
            hostConfiguration.TelemetryInitializers.Add(new CallbackInitializer(item => item.Context.Operation.Name = customName));
            var options = V2Options();
            options.Tracing.IncludeInstanceIdInOperationName = includeInstanceId;
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add, options);
            activator.Initialize(NullLogger.Instance);
            Activity.Current = null;
            using var activity = taskType == null ? null : new Activity("orchestration:Order")
                .SetTag(Schema.Task.Type, taskType)
                .SetTag(Schema.Task.Operation, operation)
                .SetTag(Schema.Task.InstanceId, "order-123")
                .Start();

            var client = new TelemetryClient(activator.TelemetryConfiguration);
            var request = NewRequest();
            client.Initialize(request);
            Assert.Equal(expectedName, request.Context.Operation.Name);
            client.TrackRequest(request);

            Assert.Same(request, Assert.Single(captured));
            Assert.Equal(expectedName, request.Context.Operation.Name);
            Assert.Equal("orchestration:Order", request.Name);
            Assert.IsType<DurableTaskInstanceIdTelemetryInitializer>(activator.TelemetryConfiguration.TelemetryInitializers.Last());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Track_InitializerFailureUsesSdkDiagnosticsAndContinuesWithoutRollbackOrRetry()
        {
            using var hostConfiguration = CreateHostConfiguration();
            var calls = new List<string>();
            hostConfiguration.TelemetryInitializers.Add(new CallbackInitializer(item => calls.Add("before")));
            hostConfiguration.TelemetryInitializers.Add(new CallbackInitializer(item =>
            {
                calls.Add("throw");
                item.Context.Cloud.RoleName = "partial-enrichment";
                throw new InvalidOperationException("host-initializer-test-failure");
            }));
            hostConfiguration.TelemetryInitializers.Add(new CallbackInitializer(item =>
            {
                calls.Add("after");
                item.Context.GlobalProperties["after"] = "present";
            }));
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add);
            activator.Initialize(NullLogger.Instance);
            using var diagnostics = new SdkDiagnosticListener();

            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            Assert.Equal(new[] { "before", "throw", "after" }, calls);
            var request = Assert.Single(captured);
            Assert.Equal("partial-enrichment", request.Context.Cloud.RoleName);
            Assert.Equal("present", request.Context.GlobalProperties["after"]);
            var diagnostic = Assert.Single(diagnostics.Events, item =>
                item.EventId == 14 && item.Payload.Any(value => value?.ToString().Contains("host-initializer-test-failure") == true));
            Assert.Equal(EventLevel.Error, diagnostic.Level);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Track_TrustedCustomInitializerMayDeliberatelyChangeProducerFields()
        {
            using var hostConfiguration = CreateHostConfiguration();
            hostConfiguration.TelemetryInitializers.Add(new CallbackInitializer(item =>
            {
                var request = (RequestTelemetry)item;
                request.Id = "custom-span";
                request.Context.Operation.Id = "custom-trace";
                request.Context.Operation.ParentId = "custom-parent";
                request.Duration = TimeSpan.FromSeconds(5);
                request.Success = true;
            }));
            var captured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, captured.Add);
            activator.Initialize(NullLogger.Instance);

            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            var request = Assert.IsType<RequestTelemetry>(Assert.Single(captured));
            Assert.Equal("custom-span", request.Id);
            Assert.Equal("custom-trace", request.Context.Operation.Id);
            Assert.Equal("custom-parent", request.Context.Operation.ParentId);
            Assert.Equal(TimeSpan.FromSeconds(5), request.Duration);
            Assert.True(request.Success);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_FreezesMembershipWithoutChangingHostPipelineOrClientContext()
        {
            var hostCaptured = new List<ITelemetry>();
            using var hostConfiguration = CreateHostConfiguration(hostCaptured.Add);
            var original = new CallbackInitializer(item => item.Context.GlobalProperties["original"] = "present");
            var late = new CallbackInitializer(item => item.Context.GlobalProperties["late"] = "present");
            hostConfiguration.TelemetryInitializers.Add(original);
            int processorCalls = 0;
            var processor = new Mock<ITelemetryProcessor>();
            hostConfiguration.TelemetryProcessorChainBuilder.Use(next =>
            {
                processor.Setup(value => value.Process(It.IsAny<ITelemetry>()))
                    .Callback<ITelemetry>(item =>
                    {
                        processorCalls++;
                        next.Process(item);
                    });
                return processor.Object;
            }).Build();
            var hostClient = new TelemetryClient(hostConfiguration);
            hostClient.Context.Cloud.RoleName = "host-client-only";
            hostClient.Context.GlobalProperties["host-client-only"] = "present";
            var originalChannel = hostConfiguration.TelemetryChannel;
            var originalProcessors = hostConfiguration.TelemetryProcessors.ToArray();
            var originalInitializers = hostConfiguration.TelemetryInitializers.ToArray();
            var durableCaptured = new List<ITelemetry>();
            using var activator = CreateActivator(hostConfiguration, durableCaptured.Add);

            activator.Initialize(NullLogger.Instance);

            Assert.Same(originalChannel, hostConfiguration.TelemetryChannel);
            Assert.Equal<ITelemetryProcessor>(originalProcessors, hostConfiguration.TelemetryProcessors);
            Assert.Equal<ITelemetryInitializer>(originalInitializers, hostConfiguration.TelemetryInitializers);
            Assert.NotSame(originalChannel, activator.TelemetryConfiguration.TelemetryChannel);
            Assert.DoesNotContain(processor.Object, activator.TelemetryConfiguration.TelemetryProcessors);
            hostConfiguration.TelemetryInitializers.Remove(original);
            hostConfiguration.TelemetryInitializers.Add(late);
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            var durable = Assert.Single(durableCaptured);
            Assert.Equal("present", durable.Context.GlobalProperties["original"]);
            Assert.False(durable.Context.GlobalProperties.ContainsKey("late"));
            Assert.False(durable.Context.GlobalProperties.ContainsKey("host-client-only"));
            Assert.Null(durable.Context.Cloud.RoleName);
            Assert.Equal(0, processorCalls);
            Assert.Empty(hostCaptured);

            hostClient.TrackDependency(new DependencyTelemetry { Name = "SELECT orders", Type = "SQL", Success = true });

            var hostDependency = Assert.IsType<DependencyTelemetry>(Assert.Single(hostCaptured));
            Assert.Equal("host-client-only", hostDependency.Context.Cloud.RoleName);
            Assert.Equal("present", hostDependency.Context.GlobalProperties["host-client-only"]);
            Assert.Equal("present", hostDependency.Context.GlobalProperties["late"]);
            Assert.False(hostDependency.Context.GlobalProperties.ContainsKey("original"));
            Assert.Equal(1, processorCalls);
            Assert.Equal("SELECT orders", hostDependency.Name);
            Assert.Equal("SQL", hostDependency.Type);
            Assert.True(hostDependency.Success);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Track_BorrowsHostInstancesForConcurrentUseAndDoesNotDisposeThem()
        {
            var hostCaptured = new ConcurrentQueue<ITelemetry>();
            using var hostConfiguration = CreateHostConfiguration(hostCaptured.Enqueue);
            var initializer = new Mock<ITelemetryInitializer>();
            var disposable = initializer.As<IDisposable>();
            var module = initializer.As<ITelemetryModule>();
            int initializeCalls = 0;
            int channelDisposals = 0;
            ((NoOpTelemetryChannel)hostConfiguration.TelemetryChannel).OnDispose = () => channelDisposals++;
            initializer.Setup(value => value.Initialize(It.IsAny<ITelemetry>()))
                .Callback<ITelemetry>(item =>
                {
                    Interlocked.Increment(ref initializeCalls);
                    item.Context.Cloud.RoleName = "shared-enricher";
                });
            hostConfiguration.TelemetryInitializers.Add(initializer.Object);
            var durableCaptured = new ConcurrentQueue<ITelemetry>();
            using (var activator = CreateActivator(hostConfiguration, durableCaptured.Enqueue))
            {
                activator.Initialize(NullLogger.Instance);
                Assert.Contains(initializer.Object, activator.TelemetryConfiguration.TelemetryInitializers);
                var durableClient = new TelemetryClient(activator.TelemetryConfiguration);
                var hostClient = new TelemetryClient(hostConfiguration);

                Parallel.For(0, 32, index =>
                {
                    durableClient.TrackRequest(NewRequest());
                    hostClient.TrackRequest(NewRequest());
                });

                Assert.Equal(32, durableCaptured.Count);
                Assert.Equal(32, hostCaptured.Count);
                Assert.Equal(64, initializeCalls);
                Assert.All(durableCaptured.Concat(hostCaptured), item => Assert.Equal("shared-enricher", item.Context.Cloud.RoleName));
            }

            disposable.Verify(value => value.Dispose(), Times.Never);
            module.Verify(value => value.Initialize(It.IsAny<TelemetryConfiguration>()), Times.Never);
            Assert.Equal(0, channelDisposals);
            new TelemetryClient(hostConfiguration).TrackRequest(NewRequest());
            Assert.Equal(33, hostCaptured.Count);
            Assert.Equal(65, initializeCalls);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_NewHostGenerationBorrowsOnlyItsOwnHostInstanceEnricher()
        {
            ITelemetryInitializer previousInitializer = null;
            foreach (string hostInstanceId in new[] { "host-generation-one", "host-generation-two" })
            {
                using var hostConfiguration = CreateHostConfiguration();

                // Representative host-instance enrichment; the real Script initializer requires full-host release validation.
                var initializer = new CallbackInitializer(item => item.Context.GlobalProperties["HostInstanceId"] = hostInstanceId);
                hostConfiguration.TelemetryInitializers.Add(initializer);
                var captured = new List<ITelemetry>();
                using var activator = CreateActivator(hostConfiguration, captured.Add);

                activator.Initialize(NullLogger.Instance);
                new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

                Assert.Same(initializer, activator.TelemetryConfiguration.TelemetryInitializers[1]);
                if (previousInitializer != null)
                {
                    Assert.DoesNotContain(previousInitializer, activator.TelemetryConfiguration.TelemetryInitializers);
                }

                Assert.Equal(hostInstanceId, Assert.Single(captured).Context.GlobalProperties["HostInstanceId"]);
                previousInitializer = initializer;
            }
        }

        public void Dispose()
        {
            CorrelationSettings.Current.EnableDistributedTracing = this.originalDistributedTracing;
            CorrelationSettings.Current.Protocol = this.originalProtocol;
            Activity.DefaultIdFormat = this.originalIdFormat;
            Activity.ForceDefaultIdFormat = this.originalForceIdFormat;
            Activity.Current = this.originalActivity;
        }

        private static DurableTaskOptions V2Options()
        {
            return new DurableTaskOptions
            {
                Tracing = new TraceOptions
                {
                    DistributedTracingEnabled = true,
                    Version = DurableDistributedTracingVersion.V2,
                    UseHostTelemetryInitializers = true,
                },
            };
        }

        private static TelemetryConfiguration CreateHostConfiguration(Action<ITelemetry> onSend = null)
        {
            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.TelemetryChannel = new NoOpTelemetryChannel { OnSend = onSend ?? (_ => { }) };
            configuration.ConnectionString = ConnectionString;
            return configuration;
        }

        private static TelemetryActivator CreateActivator(
            TelemetryConfiguration hostConfiguration,
            Action<ITelemetry> onSend = null,
            DurableTaskOptions options = null)
        {
            var resolver = new Mock<INameResolver>();
            resolver.Setup(value => value.Resolve("APPLICATIONINSIGHTS_CONNECTION_STRING")).Returns(ConnectionString);
            return new TelemetryActivator(
                Microsoft.Extensions.Options.Options.Create(options ?? V2Options()),
                resolver.Object,
                hostConfiguration)
            {
                OnSend = onSend ?? (_ => { }),
            };
        }

        private static IHost BuildWebJobsHost(
            IHttpContextAccessor httpContextAccessor = null,
            Action<IServiceCollection> configureServices = null,
            Action<TelemetryConfiguration> additionalConfiguration = null,
            bool useSeparateLoadContext = false)
        {
            if (useSeparateLoadContext)
            {
                // Run the same real host setup against private WebJobs/ASP.NET AI copies, sharing AI core.
                var testType = IsolatedHostTestAssembly.Value.GetType(typeof(HostTelemetryInitializerTests).FullName, throwOnError: true);
                return (IHost)testType.GetMethod(nameof(BuildWebJobsHost), BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { httpContextAccessor, configureServices, additionalConfiguration, false });
            }

            return new HostBuilder()
                .ConfigureLogging(logging => logging.AddApplicationInsightsWebJobs(
                    options =>
                    {
                        options.ConnectionString = ConnectionString;
                        options.EnableDependencyTracking = false;
                        options.EnablePerformanceCountersCollection = false;
                        options.EnableLiveMetrics = false;
                        options.EnableAutocollectedMetricsExtractor = false;
                        options.SamplingSettings = null;
                        options.DiagnosticsEventListenerLogLevel = null;
                    },
                    additionalConfiguration))
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITelemetryChannel>(new NoOpTelemetryChannel());
                    if (httpContextAccessor != null)
                    {
                        services.AddSingleton(httpContextAccessor);
                    }

                    configureServices?.Invoke(services);
                })
                .Build();
        }

        private static Assembly LoadIsolatedHostTestAssembly()
        {
            // Like the Functions host, keep one non-collectible context for these SDK assemblies:
            // SDK process-wide listeners can retain their types even after an individual host is disposed.
            var context = new AssemblyLoadContext("HostTelemetryInitializerTests", isCollectible: false);
            context.LoadFromAssemblyPath(typeof(ApplicationInsightsLoggerOptions).Assembly.Location);
            context.LoadFromAssemblyPath(typeof(ClientIpHeaderTelemetryInitializer).Assembly.Location);
            return context.LoadFromAssemblyPath(typeof(HostTelemetryInitializerTests).Assembly.Location);
        }

        private static ITelemetryInitializer CreateInitializerLookalike(string fullName, AssemblyName identity)
        {
            // Synthetic metadata exercises version/key differences without adding SDK package versions.
            var assembly = AssemblyBuilder.DefineDynamicAssembly(identity, AssemblyBuilderAccess.RunAndCollect);
            var type = assembly.DefineDynamicModule(identity.Name).DefineType(fullName, TypeAttributes.Public);
            type.AddInterfaceImplementation(typeof(ITelemetryInitializer));
            type.DefineDefaultConstructor(MethodAttributes.Public);
            var initialize = type.DefineMethod(
                nameof(ITelemetryInitializer.Initialize),
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(void),
                new[] { typeof(ITelemetry) });
            initialize.GetILGenerator().Emit(OpCodes.Ret);
            return (ITelemetryInitializer)Activator.CreateInstance(type.CreateType());
        }

        private static RequestTelemetry NewRequest()
        {
            var request = new RequestTelemetry(
                "orchestration:Order",
                new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                TimeSpan.FromMilliseconds(123),
                "500",
                false)
            {
                Id = "1111111111111111",
            };
            request.Context.Operation.Id = "0123456789abcdef0123456789abcdef";
            request.Context.Operation.ParentId = "abcdefabcdefabcd";
            return request;
        }

        private sealed class CallbackInitializer : ITelemetryInitializer
        {
            private readonly Action<ITelemetry> initialize;

            public CallbackInitializer(Action<ITelemetry> initialize)
            {
                this.initialize = initialize;
            }

            public void Initialize(ITelemetry telemetry) => this.initialize(telemetry);
        }

        private sealed class WebJobsTelemetryInitializer : ITelemetryInitializer
        {
            public void Initialize(ITelemetry telemetry)
            {
            }
        }

        private sealed class CustomOperationCorrelationInitializer : OperationCorrelationTelemetryInitializer
        {
        }

        private sealed class CustomClientIpInitializer : ClientIpHeaderTelemetryInitializer
        {
            public CustomClientIpInitializer(IHttpContextAccessor httpContextAccessor)
                : base(httpContextAccessor)
            {
            }
        }

        private sealed class SdkDiagnosticListener : EventListener
        {
            public ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new ConcurrentQueue<EventWrittenEventArgs>();

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Microsoft-ApplicationInsights-Core")
                {
                    this.EnableEvents(eventSource, EventLevel.Error, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                this.Events?.Enqueue(eventData);
            }
        }
    }
}

#pragma warning disable SA1402, SA1403
namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    // Same full name as the internal WebJobs initializer, but a different defining assembly.
    internal sealed class WebJobsTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
        }
    }
    #pragma warning restore SA1402, SA1403
}
