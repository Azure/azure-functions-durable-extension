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
        private const string AzureStorageResource = "https://storage.azure.com/";
        private const string AzureStorageResourceScope = AzureStorageResource + ".default";

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
            this.traceHelper = new EndToEndTraceHelper(loggerFactory?.CreateLogger(DurableTaskExtension.LoggerCategoryName) ?? throw new ArgumentNullException(nameof(loggerFactory)), false);
        }

        internal event Action<TokenRenewalState> Renewing;

        internal event Action<NewTokenAndFrequency> Renewed;

        internal event Action<int, NewTokenAndFrequency, Exception> RenewalFailed;

        /// <inheritdoc />
        public AppAuthTokenCredential Create(IConfiguration configuration, CancellationToken cancellationToken = default) =>
            this.Create(configuration, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), cancellationToken);

        /// <inheritdoc />
        public AppAuthTokenCredential Create(IConfiguration configuration, TimeSpan tokenRefreshOffset, TimeSpan tokenRefreshRetryDelay, CancellationToken cancellationToken = default)
        {
            var context = new TokenRequestContext(new[] { AzureStorageResourceScope }, null);

            AzureTokenCredential tokenCredential = this.componentFactory.CreateTokenCredential(configuration);

            AccessToken value;
            this.traceHelper.RetrievingToken(this.hubName, AzureStorageResource);
            try
            {
                value = tokenCredential.GetToken(context, cancellationToken);
            }
            catch (Exception ex)
            {
                this.traceHelper.TokenRetrievalFailed(this.hubName, AzureStorageResource, ex);
                throw;
            }

            var state = new TokenRenewalState
            {
                Context = context,
                Credential = tokenCredential,
                Previous = value,
                RefreshDelay = tokenRefreshRetryDelay,
                RefreshOffset = tokenRefreshOffset,
            };

            // The token credential will make background callbacks to renew the token.
            // We suppress async flow to avoid logging scope from being captured as we do not know
            // where this will be called from first.
            using AsyncFlowControl flowControl = System.Threading.ExecutionContext.SuppressFlow();
            return new AppAuthTokenCredential(
                value.Token,
                (o, t) => this.RenewTokenAsync((TokenRenewalState)o, t),
                state,
                GetRenewalFrequency(value, tokenRefreshOffset));
        }

        private async Task<NewTokenAndFrequency> RenewTokenAsync(TokenRenewalState state, CancellationToken cancellationToken)
        {
            // First, check if the credential is being disposed
            cancellationToken.ThrowIfCancellationRequested();

            this.OnRenewing(state);

            NewTokenAndFrequency next;
            AccessToken result;
            try
            {
                result = await state.Credential.GetTokenAsync(state.Context, cancellationToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                throw;
            }
            catch (Exception e)
            {
                // Attempt to renew the token again after the configured delay has elapsed
                next = new NewTokenAndFrequency(state.Previous.Token, state.RefreshDelay);
                this.OnRenewalFailed(++state.Attempts, next, e);
                return next;
            }

            // Save the token in case the next renewal results in an exception
            state.Attempts = 0;
            state.Previous = result;
            next = new NewTokenAndFrequency(result.Token, GetRenewalFrequency(result, state.RefreshOffset));
            this.OnRenewed(next);

            return next;
        }

        private void OnRenewing(TokenRenewalState state)
        {
            this.traceHelper.RetrievingToken(this.hubName, AzureStorageResource);
            this.Renewing?.Invoke(state);
        }

        private void OnRenewed(NewTokenAndFrequency next) =>
            this.Renewed?.Invoke(next);

        private void OnRenewalFailed(int attempt, NewTokenAndFrequency next, Exception exception)
        {
            this.traceHelper.TokenRenewalFailed(this.hubName, AzureStorageResource, attempt, next.Frequency.GetValueOrDefault(), exception);
            this.RenewalFailed?.Invoke(attempt, next, exception);
        }

        private static TimeSpan GetRenewalFrequency(AccessToken accessToken, TimeSpan refreshOffset)
        {
            // If the token expires within the offset duration,
            // then adjust the value to zero so we can immediately renew it.
            // Eg. The token expires in 10 minutes, but we typically try to renew it 30 minutes before expiry
            TimeSpan frequency = accessToken.ExpiresOn - DateTimeOffset.UtcNow - refreshOffset;
            return frequency < TimeSpan.Zero ? TimeSpan.Zero : frequency;
        }

        internal sealed class TokenRenewalState
        {
            public int Attempts { get; set; }

            public TokenRequestContext Context { get; set; }

            public AzureTokenCredential Credential { get; set; }

            public AccessToken Previous { get; set; }

            public TimeSpan RefreshDelay { get; set; }

            public TimeSpan RefreshOffset { get; set; }
        }
    }
}
