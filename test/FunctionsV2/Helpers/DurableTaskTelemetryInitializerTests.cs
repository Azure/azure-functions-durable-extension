// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Collection("Non-Parallel Collection")]
    public class DurableTaskTelemetryInitializerTests
    {
        private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void AddDurableTaskTelemetryInitializer_RejectsNullArguments()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentNullException>(
                () => DurableTaskJobHostConfigurationExtensions.AddDurableTaskTelemetryInitializer(
                    null,
                    Mock.Of<ITelemetryInitializer>()));
            Assert.Throws<ArgumentNullException>(
                () => services.AddDurableTaskTelemetryInitializer((ITelemetryInitializer)null));
            Assert.Throws<ArgumentNullException>(
                () => services.AddDurableTaskTelemetryInitializer(
                    (Func<IServiceProvider, ITelemetryInitializer>)null));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_HostOnlyInitializerDoesNotLeakIntoDurablePipeline()
        {
            var hostOnly = new CallbackInitializer(
                item => item.Context.Cloud.RoleName = "host-only");
            using var hostConfiguration = CreateHostConfiguration();
            hostConfiguration.TelemetryInitializers.Add(hostOnly);
            using IHost host = BuildHost(V2Options(), hostConfiguration: hostConfiguration);
            var captured = new ConcurrentQueue<ITelemetry>();
            TelemetryActivator activator = GetActivator(host, captured.Enqueue);

            activator.Initialize(NullLogger.Instance);
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            RequestTelemetry request = Assert.IsType<RequestTelemetry>(
                Assert.Single(captured, item => item is RequestTelemetry telemetry && telemetry.Name == "orchestration:Order"));
            Assert.Null(request.Context.Cloud.RoleName);
            Assert.DoesNotContain(hostOnly, activator.TelemetryConfiguration.TelemetryInitializers);
            Assert.Equal(2, activator.TelemetryConfiguration.TelemetryInitializers.Count);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_ExplicitSharedSingletonUsesDiAndPreservesOrderAndMultiplicity()
        {
            var calls = new ConcurrentQueue<string>();
            var shared = new CallbackInitializer(item =>
            {
                calls.Enqueue("shared");
                item.Context.Cloud.RoleName = "orders-functions";
            });
            var second = new CallbackInitializer(item =>
            {
                calls.Enqueue("second");
                ((ISupportProperties)item).Properties["environment"] = "production";
            });
            using var hostConfiguration = CreateHostConfiguration();
            hostConfiguration.TelemetryInitializers.Add(shared);
            int factoryCalls = 0;
            using IHost host = BuildHost(
                V2Options(),
                builder =>
                {
                    builder.Services.AddDurableTaskTelemetryInitializer(
                        services =>
                        {
                            Interlocked.Increment(ref factoryCalls);
                            return services.GetRequiredService<CallbackInitializer>();
                        });
                    builder.Services.AddDurableTaskTelemetryInitializer(second);
                    builder.Services.AddDurableTaskTelemetryInitializer(
                        services => services.GetRequiredService<CallbackInitializer>());
                },
                services => services.AddSingleton(shared),
                hostConfiguration);
            var captured = new ConcurrentQueue<ITelemetry>();
            TelemetryActivator activator = GetActivator(host, captured.Enqueue);

            activator.Initialize(NullLogger.Instance);
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            Assert.Equal(1, factoryCalls);
            Assert.Equal(new[] { "shared", "second", "shared" }, calls);
            Assert.Equal<ITelemetryInitializer>(
                new ITelemetryInitializer[] { shared, second, shared },
                activator.TelemetryConfiguration.TelemetryInitializers.Skip(1).Take(3));
            RequestTelemetry request = Assert.IsType<RequestTelemetry>(
                Assert.Single(captured, item => item is RequestTelemetry telemetry && telemetry.Name == "orchestration:Order"));
            Assert.Equal("orders-functions", request.Context.Cloud.RoleName);
            Assert.Equal("production", request.Properties["environment"]);
            Assert.Equal("orchestration:Order", request.Context.Operation.Name);
            Assert.Contains(shared, hostConfiguration.TelemetryInitializers);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_ExplicitStandardInitializerCanBeSharedWithHost()
        {
            var shared = new OperationCorrelationTelemetryInitializer();
            using var hostConfiguration = CreateHostConfiguration();
            hostConfiguration.TelemetryInitializers.Add(shared);
            using IHost host = BuildHost(
                V2Options(),
                builder => builder.Services.AddDurableTaskTelemetryInitializer(shared),
                hostConfiguration: hostConfiguration);
            TelemetryActivator activator = GetActivator(host);

            activator.Initialize(NullLogger.Instance);

            Assert.Contains(shared, hostConfiguration.TelemetryInitializers);
            Assert.Contains(shared, activator.TelemetryConfiguration.TelemetryInitializers);
            Assert.Equal(
                2,
                activator.TelemetryConfiguration.TelemetryInitializers.Count(
                    initializer => initializer is OperationCorrelationTelemetryInitializer));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, DurableDistributedTracingVersion.V2)]
        [InlineData(true, DurableDistributedTracingVersion.V1)]
        [InlineData(true, DurableDistributedTracingVersion.None)]
        public void Initialize_InactiveRegistrationDoesNotResolveFactory(
            bool tracingEnabled,
            DurableDistributedTracingVersion version)
        {
            int factoryCalls = 0;
            var options = new DurableTaskOptions
            {
                Tracing = new TraceOptions
                {
                    DistributedTracingEnabled = tracingEnabled,
                    Version = version,
                },
            };
            using IHost host = BuildHost(
                options,
                builder => builder.Services.AddDurableTaskTelemetryInitializer(_ =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    return new CallbackInitializer(_ => { });
                }));
            TelemetryActivator activator = GetActivator(host);

            activator.Initialize(NullLogger.Instance);

            Assert.Equal(0, factoryCalls);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_ResolvesFactoryOnceAcrossRepeatedInitialization()
        {
            int factoryCalls = 0;
            var initializer = new CallbackInitializer(_ => { });
            using IHost host = BuildHost(
                V2Options(),
                builder => builder.Services.AddDurableTaskTelemetryInitializer(_ =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    return initializer;
                }));
            TelemetryActivator activator = GetActivator(host);

            activator.Initialize(NullLogger.Instance);
            activator.Initialize(NullLogger.Instance);

            Assert.Equal(1, factoryCalls);
            Assert.Contains(initializer, activator.TelemetryConfiguration.TelemetryInitializers);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_NewHostGetsANewRegistrationSnapshot()
        {
            int factoryCalls = 0;
            var initializer = new CallbackInitializer(_ => { });
            Action<IWebJobsBuilder> register = builder =>
                builder.Services.AddDurableTaskTelemetryInitializer(_ =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    return initializer;
                });

            using (IHost firstHost = BuildHost(V2Options(), register))
            {
                TelemetryActivator firstActivator = GetActivator(firstHost);
                firstActivator.Initialize(NullLogger.Instance);
                firstActivator.Initialize(NullLogger.Instance);
                Assert.Contains(initializer, firstActivator.TelemetryConfiguration.TelemetryInitializers);
            }

            using (IHost secondHost = BuildHost(V2Options(), register))
            {
                TelemetryActivator secondActivator = GetActivator(secondHost);
                secondActivator.Initialize(NullLogger.Instance);
                Assert.Contains(initializer, secondActivator.TelemetryConfiguration.TelemetryInitializers);
            }

            using (IHost hostWithoutRegistration = BuildHost(V2Options()))
            {
                TelemetryActivator activatorWithoutRegistration = GetActivator(hostWithoutRegistration);
                activatorWithoutRegistration.Initialize(NullLogger.Instance);
                Assert.DoesNotContain(initializer, activatorWithoutRegistration.TelemetryConfiguration.TelemetryInitializers);
            }

            Assert.Equal(2, factoryCalls);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_NullFactoryResultSurfacesConfigurationError()
        {
            using IHost host = BuildHost(
                V2Options(),
                builder => builder.Services.AddDurableTaskTelemetryInitializer(_ => null));
            TelemetryActivator activator = GetActivator(host);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => activator.Initialize(NullLogger.Instance));

            Assert.Contains("factory at index 0 returned null", exception.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_FactoryExceptionIsNotSwallowed()
        {
            var expected = new InvalidOperationException("registration failed");
            using IHost host = BuildHost(
                V2Options(),
                builder => builder.Services.AddDurableTaskTelemetryInitializer(_ => throw expected));
            TelemetryActivator activator = GetActivator(host);

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                () => activator.Initialize(NullLogger.Instance));

            Assert.Same(expected, actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Track_InitializerFailureUsesSdkContinuationBehavior()
        {
            var calls = new ConcurrentQueue<string>();
            var failing = new CallbackInitializer(item =>
            {
                calls.Enqueue("failing");
                ((ISupportProperties)item).Properties["before-failure"] = "present";
                throw new InvalidOperationException("expected");
            });
            var later = new CallbackInitializer(item =>
            {
                calls.Enqueue("later");
                ((ISupportProperties)item).Properties["after-failure"] = "present";
            });
            using IHost host = BuildHost(
                V2Options(),
                builder =>
                {
                    builder.Services.AddDurableTaskTelemetryInitializer(failing);
                    builder.Services.AddDurableTaskTelemetryInitializer(later);
                });
            var captured = new ConcurrentQueue<ITelemetry>();
            TelemetryActivator activator = GetActivator(host, captured.Enqueue);
            activator.Initialize(NullLogger.Instance);

            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            RequestTelemetry request = Assert.IsType<RequestTelemetry>(
                Assert.Single(captured, item => item is RequestTelemetry telemetry && telemetry.Name == "orchestration:Order"));
            Assert.Equal(new[] { "failing", "later" }, calls);
            Assert.Equal("present", request.Properties["before-failure"]);
            Assert.Equal("present", request.Properties["after-failure"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Track_CustomMutationRunsBeforeDurableNamingInitializer()
        {
            var initializer = new CallbackInitializer(item =>
            {
                var request = (RequestTelemetry)item;
                request.Context.Operation.Name = "custom-order";
                request.Context.Location.Ip = "203.0.113.7";
                request.ResponseCode = "202";
                request.Success = true;
            });
            DurableTaskOptions options = V2Options();
            options.Tracing.IncludeInstanceIdInOperationName = true;
            using IHost host = BuildHost(
                options,
                builder => builder.Services.AddDurableTaskTelemetryInitializer(initializer));
            var captured = new ConcurrentQueue<ITelemetry>();
            TelemetryActivator activator = GetActivator(host, captured.Enqueue);
            activator.Initialize(NullLogger.Instance);
            using var activity = new Activity("orchestration:Order")
                .SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration)
                .SetTag(Schema.Task.InstanceId, "order-123")
                .Start();

            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            RequestTelemetry request = Assert.IsType<RequestTelemetry>(
                Assert.Single(captured, item => item is RequestTelemetry telemetry && telemetry.Name == "orchestration:Order"));
            Assert.Equal("orchestration:Order", request.Name);
            Assert.Equal("custom-order (order-123)", request.Context.Operation.Name);
            Assert.Equal("203.0.113.7", request.Context.Location.Ip);
            Assert.Equal("202", request.ResponseCode);
            Assert.True(request.Success);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Track_ConcurrentSharedInitializerIsNotOwnedOrInitializedAsModule()
        {
            var initializer = new Mock<ITelemetryInitializer>();
            var disposable = initializer.As<IDisposable>();
            var module = initializer.As<ITelemetryModule>();
            int initializeCalls = 0;
            initializer.Setup(value => value.Initialize(It.IsAny<ITelemetry>()))
                .Callback(() => Interlocked.Increment(ref initializeCalls));
            using IHost host = BuildHost(
                V2Options(),
                builder => builder.Services.AddDurableTaskTelemetryInitializer(initializer.Object));
            var captured = new ConcurrentQueue<ITelemetry>();
            using (TelemetryActivator activator = GetActivator(host, captured.Enqueue))
            {
                activator.Initialize(NullLogger.Instance);
                var client = new TelemetryClient(activator.TelemetryConfiguration);

                System.Threading.Tasks.Parallel.For(0, 32, _ => client.TrackRequest(NewRequest()));
            }

            Assert.Equal(32, captured.OfType<RequestTelemetry>().Count(item => item.Name == "orchestration:Order"));
            Assert.Equal(32, initializeCalls);
            disposable.Verify(value => value.Dispose(), Times.Never);
            module.Verify(value => value.Initialize(It.IsAny<TelemetryConfiguration>()), Times.Never);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_DoesNotCopyHostProcessorsOrClientContext()
        {
            using var hostConfiguration = CreateHostConfiguration();
            var processor = new Mock<ITelemetryProcessor>();
            hostConfiguration.TelemetryProcessorChainBuilder.Use(_ => processor.Object).Build();
            var hostClient = new TelemetryClient(hostConfiguration);
            hostClient.Context.Cloud.RoleName = "host-client";
            using IHost host = BuildHost(V2Options(), hostConfiguration: hostConfiguration);
            var captured = new ConcurrentQueue<ITelemetry>();
            TelemetryActivator activator = GetActivator(host, captured.Enqueue);

            activator.Initialize(NullLogger.Instance);
            new TelemetryClient(activator.TelemetryConfiguration).TrackRequest(NewRequest());

            Assert.DoesNotContain(processor.Object, activator.TelemetryConfiguration.TelemetryProcessors);
            RequestTelemetry request = Assert.IsType<RequestTelemetry>(
                Assert.Single(captured, item => item is RequestTelemetry telemetry && telemetry.Name == "orchestration:Order"));
            Assert.Null(request.Context.Cloud.RoleName);
        }

        private static IHost BuildHost(
            DurableTaskOptions options,
            Action<IWebJobsBuilder> configureWebJobs = null,
            Action<IServiceCollection> configureServices = null,
            TelemetryConfiguration hostConfiguration = null)
        {
            var resolver = new Mock<INameResolver>();
            resolver.Setup(value => value.Resolve("APPLICATIONINSIGHTS_CONNECTION_STRING"))
                .Returns(ConnectionString);
            return new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder.AddDurableTask(Microsoft.Extensions.Options.Options.Create(options));
                    configureWebJobs?.Invoke(builder);
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<INameResolver>(resolver.Object);
                    if (hostConfiguration != null)
                    {
                        services.AddSingleton(hostConfiguration);
                    }

                    configureServices?.Invoke(services);
                })
                .Build();
        }

        private static TelemetryActivator GetActivator(IHost host, Action<ITelemetry> onSend = null)
        {
            var activator = Assert.IsType<TelemetryActivator>(
                host.Services.GetRequiredService<ITelemetryActivator>());
            activator.OnSend = onSend ?? (_ => { });
            return activator;
        }

        private static TelemetryConfiguration CreateHostConfiguration()
        {
            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.TelemetryChannel = new NoOpTelemetryChannel();
            configuration.ConnectionString = ConnectionString;
            return configuration;
        }

        private static DurableTaskOptions V2Options()
        {
            return new DurableTaskOptions
            {
                Tracing = new TraceOptions
                {
                    DistributedTracingEnabled = true,
                    Version = DurableDistributedTracingVersion.V2,
                },
            };
        }

        private static RequestTelemetry NewRequest()
        {
            return new RequestTelemetry(
                "orchestration:Order",
                new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                TimeSpan.FromMilliseconds(123),
                "500",
                false)
            {
                Id = "1111111111111111",
                Context =
                {
                    Operation =
                    {
                        Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        ParentId = "bbbbbbbbbbbbbbbb",
                    },
                },
                Properties =
                {
                    [Schema.Task.Type] = TraceActivityConstants.Orchestration,
                    [Schema.Task.InstanceId] = "order-123",
                },
            };
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
    }
}
