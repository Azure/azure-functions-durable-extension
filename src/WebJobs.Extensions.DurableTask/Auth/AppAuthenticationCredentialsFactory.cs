// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    // TODO: Replace with Azure.Identity
    internal sealed class AppAuthenticationCredentialsFactory : IStorageCredentialsFactory, IAsyncDisposable
    {
        private const string LoggerName = "Host.Triggers.DurableTask.Auth";

        private readonly Func<string, AzureServiceTokenProvider> tokenProviderFactory;
        private readonly ILogger logger;
        private readonly AsyncLock cacheLock = new AsyncLock();
        private readonly Dictionary<string, TokenCredential> cache = new Dictionary<string, TokenCredential>();
        private volatile bool disposed;

        public AppAuthenticationCredentialsFactory(ILoggerFactory loggerFactory)
            : this(s => new AzureServiceTokenProvider(s), loggerFactory)
        { }

        internal AppAuthenticationCredentialsFactory(Func<string, AzureServiceTokenProvider> tokenProviderFactory, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            this.tokenProviderFactory = tokenProviderFactory ?? throw new ArgumentNullException(nameof(tokenProviderFactory));
            this.logger = loggerFactory.CreateLogger(LoggerName);
        }

        internal event Action<TokenRenewalState> Renewing;

        internal event Action<NewTokenAndFrequency> Renewed;

        internal event Action<NewTokenAndFrequency, AzureServiceTokenProviderException> RenewalFailed;

        public Task<StorageCredentials> CreateAsync(AzureIdentityOptions options, CancellationToken cancellationToken = default) =>
            this.CreateAsync(options, TimeSpan.FromMinutes(5d), TimeSpan.FromSeconds(30d), cancellationToken);

        public async Task<StorageCredentials> CreateAsync(
            AzureIdentityOptions options,
            TimeSpan tokenRefreshOffset,
            TimeSpan tokenRefreshRetryDelay,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.ThrowIfDisposed();

            string connectionString = GetConnectionString(options);
            if (connectionString == null)
            {
                return null;
            }

            // Attempt to retrieve an existing credential from the cache, if possible.
            // Otherwise, obtain a lock and populate the cache with a new credential for the auth connection.
            if (!this.cache.TryGetValue(connectionString, out TokenCredential credential))
            {
                using (await this.cacheLock.AcquireAsync())
                {
                    this.ThrowIfDisposed();
                    if (!this.cache.TryGetValue(connectionString, out credential))
                    {
                        var state = new TokenRenewalState
                        {
                            RefreshDelay = tokenRefreshRetryDelay,
                            RefreshOffset = tokenRefreshOffset,
                            TokenProvider = this.tokenProviderFactory(connectionString),
                        };

                        NewTokenAndFrequency response = await this.RenewTokenAsync(state, cancellationToken);
                        if (response.Token == null)
                        {
                            throw new InvalidOperationException("Unable to retrieve initial access token.");
                        }

                        // Create credential and update the cache
                        credential = new TokenCredential(response.Token, (s, t) => this.RenewTokenAsync(s as TokenRenewalState, t), state, tokenRefreshOffset);
                        this.cache.Add(connectionString, credential);
                    }
                }
            }

            return new StorageCredentials(credential);
        }

        private async Task<NewTokenAndFrequency> RenewTokenAsync(TokenRenewalState state, CancellationToken cancellationToken)
        {
            this.OnRenewing(state);

            NewTokenAndFrequency next;
            AppAuthenticationResult result;
            try
            {
                result = await state.TokenProvider.GetAuthenticationResultAsync(
                    "https://storage.azure.com/",
                    forceRefresh: true,
                    cancellationToken: cancellationToken);
            }
            catch (AzureServiceTokenProviderException e)
            {
                next = new NewTokenAndFrequency(state.Token, state.RefreshDelay);
                this.logger.LogWarning(e, "Unable to refresh token. Will retry in '{Delay}'.", next.Frequency);

                this.OnRenewalFailed(next, e);
                return next;
            }

            var frequency = result.ExpiresOn - DateTimeOffset.UtcNow - state.RefreshOffset;
            if (frequency < TimeSpan.Zero)
            {
                frequency = TimeSpan.Zero;
            }

            // Save the token in case renewal results in an exception
            state.Token = result.AccessToken;
            next = new NewTokenAndFrequency(result.AccessToken, frequency);

            this.OnRenewed(next);
            return next;
        }

        private void OnRenewing(TokenRenewalState state) =>
            this.Renewing?.Invoke(state);

        private void OnRenewed(NewTokenAndFrequency next) =>
            this.Renewed?.Invoke(next);

        private void OnRenewalFailed(NewTokenAndFrequency next, AzureServiceTokenProviderException exeption) =>
            this.RenewalFailed?.Invoke(next, exeption);

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(AppAuthenticationCredentialsFactory));
            }
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (!this.disposed)
            {
                using (await this.cacheLock.AcquireAsync())
                {
                    if (!this.disposed)
                    {
                        foreach (TokenCredential tokenCredential in this.cache.Values)
                        {
                            tokenCredential.Dispose();
                        }

                        GC.SuppressFinalize(this);
                        this.disposed = true;
                    }
                }
            }
        }

        internal static string GetConnectionString(AzureIdentityOptions options)
        {
            // Here we are attempting to emulate Microsoft.Extensions.Azure.ClientFactory
            // options and behavior to make the inevitable migration to Azure.Identity seamless for consumers.
            if (options.UseManagedIdentity)
            {
                return !string.IsNullOrEmpty(options.ClientId) ? FormattableString.Invariant($"RunAs=App;AppId={options.ClientId}") : "RunAs=App";
            }
            else if (
                !string.IsNullOrWhiteSpace(options.TenantId) &&
                !string.IsNullOrWhiteSpace(options.ClientId) &&
                !string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                return FormattableString.Invariant($"RunAs=App;AppId={options.ClientId};TenantId={options.TenantId};AppKey={options.ClientSecret}");
            }
            else if (
                !string.IsNullOrWhiteSpace(options.TenantId) &&
                !string.IsNullOrWhiteSpace(options.ClientId) &&
                !string.IsNullOrWhiteSpace(options.Certificate))
            {
                return FormattableString.Invariant($"RunAs=App;AppId={options.ClientId};TenantId={options.TenantId};CertificateThumbprint={options.Certificate};CertificateStoreLocation={options.ClientCertificateStoreLocation}");
            }
            else
            {
                return null;
            }
        }

        internal sealed class TokenRenewalState
        {
            public AzureServiceTokenProvider TokenProvider { get; set; }

            public TimeSpan RefreshOffset { get; set; }

            public TimeSpan RefreshDelay { get; set; }

            public string Token { get; set; }
        }
    }
}
