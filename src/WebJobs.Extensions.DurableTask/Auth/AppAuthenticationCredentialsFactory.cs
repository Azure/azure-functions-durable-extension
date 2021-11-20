// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    // TODO: Replace with Azure.Identity
    internal sealed class AppAuthenticationCredentialsFactory : IStorageCredentialsFactory
    {
        private const string LoggerName = "Host.Triggers.DurableTask.Auth";

        private readonly ILogger logger;
        private readonly AsyncLock cacheLock = new AsyncLock();
        private readonly Dictionary<string, TokenCredential> cache = new Dictionary<string, TokenCredential>();

        public AppAuthenticationCredentialsFactory(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            this.logger = loggerFactory.CreateLogger(LoggerName);
        }

        public Task<StorageCredentials> CreateAsync(ClientIdentityOptions options, CancellationToken cancellationToken = default) =>
            this.CreateAsync(options, TimeSpan.FromMinutes(5d), TimeSpan.FromSeconds(30d), cancellationToken);

        public async Task<StorageCredentials> CreateAsync(
            ClientIdentityOptions options,
            TimeSpan tokenRefreshOffset,
            TimeSpan tokenRefreshRetryDelay,
            CancellationToken cancellationToken = default)
        {
            string connectionString = CreateTokenProviderConnectionString(options);
            using (await this.cacheLock.AcquireAsync())
            {
                if (!this.cache.TryGetValue(connectionString, out TokenCredential credential))
                {
                    var state = new TokenRenewalState
                    {
                        RefreshDelay = tokenRefreshRetryDelay,
                        RefreshOffset = tokenRefreshOffset,
                        TokenProvider = new AzureServiceTokenProvider(connectionString),
                    };

                    NewTokenAndFrequency response = await this.RenewTokenAsync(state, cancellationToken);
                    credential = new TokenCredential(response.Token, (s, t) => this.RenewTokenAsync(s as TokenRenewalState, t), state, tokenRefreshOffset);

                    // Update cache
                    this.cache.Add(connectionString, credential);
                }

                return new StorageCredentials(credential);
            }
        }

        private static string CreateTokenProviderConnectionString(ClientIdentityOptions options)
        {
            StringBuilder builder = new StringBuilder("RunAs=App");

            if (!string.IsNullOrEmpty(options.ClientId))
            {
                builder.Append($";AppId={options.ClientId}");
            }

            if (!string.IsNullOrEmpty(options.TenantId))
            {
                builder.Append($";TenantId={options.TenantId}");
            }

            if (!string.IsNullOrEmpty(options.ClientSecret))
            {
                builder.Append($";ClientSecret={options.ClientSecret}");
            }

            return builder.ToString();
        }

        private async Task<NewTokenAndFrequency> RenewTokenAsync(TokenRenewalState state, CancellationToken cancellationToken)
        {
            AppAuthenticationResult result;
            try
            {
                result = await state.TokenProvider.GetAuthenticationResultAsync(
                    "https://storage.azure.com/",
                    forceRefresh: false,
                    cancellationToken: cancellationToken);
            }
            catch (AzureServiceTokenProviderException e)
            {
                this.logger.LogWarning(e, "Unable to refresh token. Will retry in '{Delay}'.", state.RefreshDelay);
                return new NewTokenAndFrequency(state.Token, state.RefreshDelay);
            }

            var frequency = result.ExpiresOn - DateTimeOffset.UtcNow - state.RefreshOffset;
            if (frequency < TimeSpan.Zero)
            {
                frequency = TimeSpan.Zero;
            }

            // Save the token in case renewal results in an exception
            state.Token = result.AccessToken;
            return new NewTokenAndFrequency(result.AccessToken, frequency);
        }

        private sealed class TokenRenewalState
        {
            public AzureServiceTokenProvider TokenProvider { get; set; }

            public TimeSpan RefreshOffset { get; set; }

            public TimeSpan RefreshDelay { get; set; }

            public string Token { get; set; }
        }
    }
}
