// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Auth;
using AppAuthTokenCredential = Microsoft.WindowsAzure.Storage.Auth.TokenCredential;
using AzureTokenCredential = Azure.Core.TokenCredential;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    internal class AzureCredentialFactory : ITokenCredentialFactory
    {
        private const string LoggerName = "Host.Triggers.DurableTask.Auth";

        private readonly string hubName;
        private readonly AzureComponentFactory componentFactory;
        private readonly EndToEndTraceHelper traceHelper;

        public AzureCredentialFactory(
            IOptions<DurableTaskOptions> options,
            AzureComponentFactory componentFactory,
            ILoggerFactory loggerFactory)
        {
            this.hubName = options?.Value?.HubName ?? throw new ArgumentNullException(nameof(options));
            this.componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
            this.traceHelper = new EndToEndTraceHelper(loggerFactory?.CreateLogger(LoggerName) ?? throw new ArgumentNullException(nameof(loggerFactory)), false);
        }

        internal event Action<TokenRenewalState> Renewing;

        internal event Action<NewTokenAndFrequency> Renewed;

        internal event Action<NewTokenAndFrequency, Exception> RenewalFailed;

        /// <inheritdoc />
        public AppAuthTokenCredential Create(IConfiguration configuration, CancellationToken cancellationToken = default) =>
            this.Create(configuration, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), cancellationToken);

        /// <inheritdoc />
        public AppAuthTokenCredential Create(IConfiguration configuration, TimeSpan tokenRefreshOffset, TimeSpan tokenRefreshRetryDelay, CancellationToken cancellationToken = default)
        {
            var context = new TokenRequestContext(new[] { "https://storage.azure.com/.default" }, null);

            AzureTokenCredential tokenCredential = this.componentFactory.CreateTokenCredential(configuration);
            AccessToken value = tokenCredential.GetToken(context, default);
            var state = new TokenRenewalState
            {
                Context = context,
                Credential = tokenCredential,
                Previous = value,
                RefreshDelay = tokenRefreshRetryDelay,
                RefreshOffset = tokenRefreshOffset,
            };

            return new AppAuthTokenCredential(
                value.Token,
                (o, t) => this.RenewTokenAsync(o as TokenRenewalState, t),
                state,
                default);
        }

        private async Task<NewTokenAndFrequency> RenewTokenAsync(TokenRenewalState state, CancellationToken cancellationToken)
        {
            this.OnRenewing(state);

            NewTokenAndFrequency next;
            AccessToken result;
            try
            {
                result = await state.Credential.GetTokenAsync(state.Context, cancellationToken);
            }
            catch (Exception e)
            {
                next = new NewTokenAndFrequency(state.Previous.Token, state.RefreshDelay);
                this.OnRenewalFailed(next, e);
                return next;
            }

            var frequency = result.ExpiresOn - DateTimeOffset.UtcNow - state.RefreshOffset;
            if (frequency < TimeSpan.Zero)
            {
                frequency = TimeSpan.Zero;
            }

            // Save the token in case renewal results in an exception
            state.Previous = result;
            next = new NewTokenAndFrequency(result.Token, frequency);
            this.OnRenewed(next);
            return next;
        }

        private void OnRenewing(TokenRenewalState state) =>
            this.Renewing?.Invoke(state);

        private void OnRenewed(NewTokenAndFrequency next) =>
            this.Renewed?.Invoke(next);

        private void OnRenewalFailed(NewTokenAndFrequency next, Exception exception)
        {
            this.traceHelper.TokenRenewalFailed(this.hubName, "https://storage.azure.com/", next.Frequency.GetValueOrDefault(), exception);
            this.RenewalFailed?.Invoke(next, exception);
        }

        internal sealed class TokenRenewalState
        {
            public TokenRequestContext Context { get; set; }

            public AzureTokenCredential Credential { get; set; }

            public AccessToken Previous { get; set; }

            public TimeSpan RefreshDelay { get; set; }

            public TimeSpan RefreshOffset { get; set; }
        }
    }
}
