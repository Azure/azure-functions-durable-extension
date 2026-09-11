// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ApplicationInsightsTokenCredentialOptions = Microsoft.Azure.WebJobs.Logging.ApplicationInsights.TokenCredentialOptions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TelemetryActivatorTests
    {
        private const string SystemAssigned = "Authorization=AAD";
        private const string UserAssigned = "Authorization=AAD;ClientId=00000000-0000-0000-0000-000000000001";

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ApplyEntraAuthentication_ReusesHostChannel()
        {
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            var hostChannel = new NoOpTelemetryChannel();
            hostConfiguration.TelemetryChannel = hostChannel;

            bool reusedHostChannel = Apply(durableConfiguration, hostConfiguration, SystemAssigned);

            Assert.True(reusedHostChannel);
            var forwarding = Assert.IsType<HostForwardingTelemetryChannel>(durableConfiguration.TelemetryChannel);
            Assert.Same(hostChannel, forwarding.HostChannel);
        }

        /// <summary>
        /// The whole point of the design: telemetry tracked on the private Durable configuration has
        /// to physically leave through the host channel, because that is what holds the credential.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ApplyEntraAuthentication_SendsTelemetryThroughHostChannel()
        {
            var forwarded = new List<ITelemetry>();
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            hostConfiguration.TelemetryChannel = new NoOpTelemetryChannel { OnSend = forwarded.Add };

            Apply(durableConfiguration, hostConfiguration, SystemAssigned);

            new ApplicationInsights.TelemetryClient(durableConfiguration)
                .TrackRequest(new RequestTelemetry("durable-span", DateTimeOffset.UtcNow, TimeSpan.Zero, "200", true));

            ITelemetry item = Assert.Single(forwarded);
            Assert.Equal("durable-span", Assert.IsType<RequestTelemetry>(item).Name);
        }

        /// <summary>
        /// Durable must never clear the credential the host installed on its own configuration.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ApplyEntraAuthentication_LeavesHostCredentialIntact()
        {
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
            hostConfiguration.TelemetryChannel = new NoOpTelemetryChannel();
            hostConfiguration.SetAzureTokenCredential(credential);

            Apply(durableConfiguration, hostConfiguration, SystemAssigned);

            Assert.Same(credential, GetCredential(hostConfiguration));
            Assert.Null(GetCredential(durableConfiguration));
        }

        /// <summary>
        /// The Durable configuration has no credential, so any endpoint it computes is the non-Entra
        /// one. Letting that reach the host channel would break host telemetry.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ApplyEntraAuthentication_DoesNotOverwriteHostChannelEndpoint()
        {
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            hostConfiguration.TelemetryChannel = new NoOpTelemetryChannel
            {
                EndpointAddress = "https://host.example.com/v2.1/track",
            };

            Apply(durableConfiguration, hostConfiguration, SystemAssigned);
            durableConfiguration.ConnectionString =
                "InstrumentationKey=11111111-2222-3333-4444-555555555555;IngestionEndpoint=https://durable.example.com/";

            Assert.Equal("https://host.example.com/v2.1/track", hostConfiguration.TelemetryChannel.EndpointAddress);
        }

        /// <summary>
        /// The host owns the channel's lifetime. Disposing the Durable configuration must not stop
        /// host telemetry.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ApplyEntraAuthentication_DoesNotDisposeHostChannel()
        {
            int disposeCalls = 0;
            var sent = new List<ITelemetry>();
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            hostConfiguration.TelemetryChannel = new NoOpTelemetryChannel
            {
                OnSend = sent.Add,
                OnDispose = () => disposeCalls++,
            };

            Apply(durableConfiguration, hostConfiguration, SystemAssigned);
            durableConfiguration.TelemetryChannel.Dispose();
            durableConfiguration.Dispose();

            Assert.Equal(0, disposeCalls);
            hostConfiguration.TelemetryChannel.Send(new RequestTelemetry());
            Assert.Single(sent);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task HostForwardingTelemetryChannel_FlushAsyncForwardsToAsyncHostChannelExactlyOnce()
        {
            using var cancellationSource = new CancellationTokenSource();
            var hostChannel = new AsyncTelemetryChannel(flushResult: true);
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            durableConfiguration.TelemetryChannel = new HostForwardingTelemetryChannel(hostChannel);
            var telemetryClient = new TelemetryClient(durableConfiguration);

            bool result = await telemetryClient.FlushAsync(cancellationSource.Token);

            Assert.True(result);
            Assert.Equal(1, hostChannel.AsyncFlushCalls);
            Assert.Equal(0, hostChannel.FlushCalls);
            Assert.Equal(cancellationSource.Token, hostChannel.LastCancellationToken);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task HostForwardingTelemetryChannel_FlushAsyncFallsBackToSyncHostChannelExactlyOnce()
        {
            int flushCalls = 0;
            var hostChannel = new NoOpTelemetryChannel { OnFlush = () => flushCalls++ };
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            durableConfiguration.TelemetryChannel = new HostForwardingTelemetryChannel(hostChannel);
            var telemetryClient = new TelemetryClient(durableConfiguration);

            bool result = await telemetryClient.FlushAsync(CancellationToken.None);

            Assert.False(result);
            Assert.Equal(1, flushCalls);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DisposeAsync_AwaitsTelemetryModulesSequentially()
        {
            var firstModule = new ControlledAsyncDisposable();
            var secondModule = new ControlledAsyncDisposable();
            var activator = new TelemetryActivator(
                Microsoft.Extensions.Options.Options.Create(new DurableTaskOptions()),
                Mock.Of<INameResolver>())
            {
                TelemetryModule = firstModule,
                WebJobsTelemetryModule = secondModule,
            };

            ValueTask disposal = activator.DisposeAsync();

            Assert.False(disposal.IsCompleted);
            Assert.Equal(1, firstModule.DisposeCalls);
            Assert.Equal(0, secondModule.DisposeCalls);

            firstModule.CompleteDisposal();
            await secondModule.DisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(disposal.IsCompleted);
            Assert.Equal(1, secondModule.DisposeCalls);

            secondModule.CompleteDisposal();
            await disposal;
        }

        /// <summary>
        /// The OnSend test hook installs its own channel. Forwarding would silently swallow it.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ApplyEntraAuthentication_PreservesExistingTestChannel()
        {
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            var testChannel = new NoOpTelemetryChannel();
            durableConfiguration.TelemetryChannel = testChannel;
            hostConfiguration.TelemetryChannel = new NoOpTelemetryChannel();

            bool reusedHostChannel = TelemetryActivator.ApplyEntraAuthentication(
                durableConfiguration,
                hostConfiguration,
                ApplicationInsightsTokenCredentialOptions.ParseAuthenticationString(SystemAssigned),
                preserveExistingChannel: true);

            Assert.False(reusedHostChannel);
            Assert.Same(testChannel, durableConfiguration.TelemetryChannel);
        }

        /// <summary>
        /// Outside the Functions host there is no channel to reuse, so Durable still has to
        /// authenticate on its own.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(SystemAssigned)]
        [InlineData(UserAssigned)]
        public void ApplyEntraAuthentication_WithoutHostConfiguration_UsesManagedIdentity(string authenticationString)
        {
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();

            bool reusedHostChannel = Apply(durableConfiguration, hostConfiguration: null, authenticationString);

            Assert.False(reusedHostChannel);
            Assert.IsType<ManagedIdentityCredential>(GetCredential(durableConfiguration));
            Assert.IsNotType<HostForwardingTelemetryChannel>(durableConfiguration.TelemetryChannel);
        }

        /// <summary>
        /// A host configuration can exist without a channel. The caller relies on the return value
        /// to warn instead of degrading silently.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ApplyEntraAuthentication_WithHostConfigurationLackingChannel_ReportsFallback()
        {
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            using TelemetryConfiguration durableConfiguration = TelemetryConfiguration.CreateDefault();
            hostConfiguration.TelemetryChannel = null;

            bool reusedHostChannel = Apply(durableConfiguration, hostConfiguration, SystemAssigned);

            Assert.False(reusedHostChannel);
            Assert.IsType<ManagedIdentityCredential>(GetCredential(durableConfiguration));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DependencyInjection_SelectsHostTelemetryConfigurationConstructor()
        {
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<DurableTaskOptions>>(
                Microsoft.Extensions.Options.Options.Create(new DurableTaskOptions()));
            services.AddSingleton(Mock.Of<INameResolver>());
            services.AddSingleton(hostConfiguration);
            services.AddSingleton<ITelemetryActivator, TelemetryActivator>();

            using ServiceProvider provider = services.BuildServiceProvider();
            TelemetryActivator activator =
                Assert.IsType<TelemetryActivator>(provider.GetRequiredService<ITelemetryActivator>());
            FieldInfo field = typeof(TelemetryActivator).GetField(
                "hostTelemetryConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(field);
            Assert.Same(hostConfiguration, field.GetValue(activator));
        }

        private static bool Apply(
            TelemetryConfiguration durableConfiguration,
            TelemetryConfiguration hostConfiguration,
            string authenticationString)
        {
            return TelemetryActivator.ApplyEntraAuthentication(
                durableConfiguration,
                hostConfiguration,
                ApplicationInsightsTokenCredentialOptions.ParseAuthenticationString(authenticationString));
        }

        /// <summary>
        /// Reads the credential the Application Insights SDK keeps internally. Only the test needs
        /// this; the production path no longer inspects it.
        /// </summary>
        private static object GetCredential(TelemetryConfiguration configuration)
        {
            PropertyInfo envelopeProperty = typeof(TelemetryConfiguration).GetProperty(
                "CredentialEnvelope",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object envelope = envelopeProperty?.GetValue(configuration);
            PropertyInfo credentialProperty = envelope?.GetType().GetProperty(
                "Credential",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return credentialProperty?.GetValue(envelope);
        }

        private sealed class AsyncTelemetryChannel : ITelemetryChannel, IAsyncFlushable
        {
            private readonly bool flushResult;

            public AsyncTelemetryChannel(bool flushResult)
            {
                this.flushResult = flushResult;
            }

            public bool? DeveloperMode { get; set; }

            public string EndpointAddress { get; set; }

            public int AsyncFlushCalls { get; private set; }

            public int FlushCalls { get; private set; }

            public CancellationToken LastCancellationToken { get; private set; }

            public void Dispose()
            {
            }

            public void Flush()
            {
                this.FlushCalls++;
            }

            public Task<bool> FlushAsync(CancellationToken cancellationToken)
            {
                this.AsyncFlushCalls++;
                this.LastCancellationToken = cancellationToken;
                return Task.FromResult(this.flushResult);
            }

            public void Send(ITelemetry item)
            {
            }
        }

        private sealed class ControlledAsyncDisposable : IAsyncDisposable
        {
            private readonly TaskCompletionSource<bool> disposalCompletion =
                new (TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> DisposalStarted { get; } =
                new (TaskCreationOptions.RunContinuationsAsynchronously);

            public int DisposeCalls { get; private set; }

            public ValueTask DisposeAsync()
            {
                this.DisposeCalls++;
                this.DisposalStarted.TrySetResult(true);
                return new ValueTask(this.disposalCompletion.Task);
            }

            public void CompleteDisposal()
            {
                this.disposalCompletion.TrySetResult(true);
            }
        }
    }
}
