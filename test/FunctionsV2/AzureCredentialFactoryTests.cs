// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Language;
using Xunit;
using Xunit.Sdk;
using AppAuthTokenCredential = Microsoft.WindowsAzure.Storage.Auth.TokenCredential;
using AzureTokenCredential = Azure.Core.TokenCredential;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class AzureCredentialFactoryTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Create()
        {
            // Create test data
            IConfiguration config = new ConfigurationBuilder().Build();
            DateTimeOffset start = DateTimeOffset.UtcNow;
            TimeSpan expiration = TimeSpan.FromSeconds(1);
            TimeSpan offset = TimeSpan.FromMinutes(10);
            TimeSpan delay = TimeSpan.FromMinutes(1);
            var expected = new AccessToken[]
            {
                new AccessToken("AAAA", start + expiration),
                new AccessToken("BBBB", start + expiration + expiration),
                new AccessToken("CCCC", start + offset + offset),
            };

            // Setup mocks
            AzureCredentialFactory factory = SetupCredentialsFactory(config, expected);

            // Assert behavior through events
            int renewingCalls = 0, renewedCalls = 0;
            factory.Renewing += s =>
            {
                renewingCalls++;
                Assert.Equal(offset, s.RefreshOffset);
                Assert.Equal(delay, s.RefreshDelay);
                Assert.Equal("https://storage.azure.com/.default", s.Context.Scopes.Single());
                Assert.Null(s.Context.TenantId);
                Assert.Equal(expected[renewingCalls - 1], s.Previous);
            };

            factory.Renewed += n =>
            {
                renewedCalls++;
                Assert.Equal(expected[renewedCalls].Token, n.Token);

                if (renewedCalls < expected.Length - 1)
                {
                    // The expirations are so short for the first entries that they will renew immediately!
                    Assert.Equal(TimeSpan.Zero, n.Frequency);
                }
                else
                {
                    // We expect to renew this token within the offset from the expiration time
                    TimeSpan max = expected[^1].ExpiresOn - start - offset;
                    Assert.True(n.Frequency <= max);
                }
            };

            factory.RenewalFailed += (n, e) => throw new XunitException($"Renewal failed unexpectedly: {e}");

            // Create the credential and assert its state
            using AppAuthTokenCredential credential = factory.Create(config, offset, delay);

            // Wait until the final renewal complete
            WaitUntilRenewal(credential, expected[^1].Token, TimeSpan.FromSeconds(30));
            Assert.Equal(expected.Length - 1, renewingCalls);
            Assert.Equal(expected.Length - 1, renewedCalls);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConnectionFailure()
        {
            // Create test data
            IConfiguration config = new ConfigurationBuilder().Build();
            DateTimeOffset start = DateTimeOffset.UtcNow;
            TimeSpan expiration = TimeSpan.FromSeconds(1);
            TimeSpan offset = TimeSpan.FromMinutes(10);
            TimeSpan delay = TimeSpan.FromSeconds(1);
            var expected = new AccessToken[]
            {
                new AccessToken("AAAA", start + expiration),
                new AccessToken("BBBB", start + expiration + expiration),
                new AccessToken("CCCC", start + offset + offset),
            };

            // Setup mocks
            AzureCredentialFactory factory = SetupCredentialsFactoryWithError(config, expected);

            // Assert behavior through events
            int renewingCalls = 0, renewedCalls = 0, failureCalls = 0;
            factory.Renewing += s => renewingCalls++;
            factory.Renewed += n => renewedCalls++;
            factory.RenewalFailed += (n, e) =>
            {
                failureCalls++;
                Assert.Equal(expected[^2].Token, n.Token);
                Assert.Equal(delay, n.Frequency);
            };

            // Create the credential and assert its state
            using AppAuthTokenCredential credential = factory.Create(config, offset, delay);

            // Wait until the final renewal complete (including the extra retry on error)
            WaitUntilRenewal(credential, expected[^1].Token, TimeSpan.FromSeconds(30));
            Assert.Equal(expected[^1].Token, credential.Token);
            Assert.Equal(expected.Length, renewingCalls);
            Assert.Equal(expected.Length - 1, renewedCalls);
            Assert.Equal(1, failureCalls);
        }

        private static AzureCredentialFactory SetupCredentialsFactory(IConfiguration config, params AccessToken[] tokens)
        {
            var mockFactory = new Mock<AzureComponentFactory>(MockBehavior.Strict);
            var mockCredential = new Mock<AzureTokenCredential>(MockBehavior.Strict);
            mockFactory.Setup(f => f.CreateTokenCredential(config)).Returns(mockCredential.Object);

            // Call pattern is a synchronous call to GetToken followed by asynchronous calls to GetTokenAsync in the callback
            mockCredential
                .SetupSequence(c =>
                    c.GetToken(
                        It.Is<TokenRequestContext>(cxt => cxt.Scopes.Single() == "https://storage.azure.com/.default"),
                        It.IsAny<CancellationToken>()))
                .Returns(tokens.First());

            ISetupSequentialResult<ValueTask<AccessToken>> asyncResult = mockCredential
                .SetupSequence(c =>
                    c.GetTokenAsync(
                        It.Is<TokenRequestContext>(cxt => cxt.Scopes.Single() == "https://storage.azure.com/.default"),
                        It.IsAny<CancellationToken>()))
                .ReturnsSequenceAsync(tokens.Skip(1));

            return new AzureCredentialFactory(Options.Create(new DurableTaskOptions()), mockFactory.Object, NullLoggerFactory.Instance);
        }

        private static AzureCredentialFactory SetupCredentialsFactoryWithError(IConfiguration config, params AccessToken[] tokens)
        {
            var mockFactory = new Mock<AzureComponentFactory>(MockBehavior.Strict);
            var mockCredential = new Mock<AzureTokenCredential>(MockBehavior.Strict);
            mockFactory.Setup(f => f.CreateTokenCredential(config)).Returns(mockCredential.Object);

            // Call pattern is a synchronous call to GetToken followed by asynchronous calls to GetTokenAsync in the callback
            mockCredential
                .SetupSequence(c =>
                    c.GetToken(
                        It.Is<TokenRequestContext>(cxt => cxt.Scopes.Single() == "https://storage.azure.com/.default"),
                        It.IsAny<CancellationToken>()))
                .Returns(tokens.First());

            // Inject an error in the 2nd to last call
            ISetupSequentialResult<ValueTask<AccessToken>> asyncResult = mockCredential
                .SetupSequence(c =>
                    c.GetTokenAsync(
                        It.Is<TokenRequestContext>(cxt => cxt.Scopes.Single() == "https://storage.azure.com/.default"),
                        It.IsAny<CancellationToken>()))
                .ReturnsSequenceAsync(tokens.Skip(1).Take(tokens.Length - 2))
                .Throws<Exception>()
                .Returns(new ValueTask<AccessToken>(tokens[^1]));

            return new AzureCredentialFactory(Options.Create(new DurableTaskOptions()), mockFactory.Object, NullLoggerFactory.Instance);
        }

        private static void WaitUntilRenewal(AppAuthTokenCredential credential, string token, TimeSpan timeout)
        {
            // We need to spin-loop to wait for the renewal as there is no "hook" for waiting until
            // the TokenCredential object has updated its Token (only after we've fetched the new value).
            using var source = new CancellationTokenSource();
            source.CancelAfter(timeout);

            while (credential.Token != token)
            {
                Assert.False(source.IsCancellationRequested, $"Could not renew credentials within {timeout}");
            }
        }
    }
}
