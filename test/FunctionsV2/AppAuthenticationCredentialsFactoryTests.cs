// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage.Auth;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class AppAuthenticationCredentialsFactoryTests
    {
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null, null, null, null, null, default(StoreLocation), null)]
        [InlineData("managedidentity", null, null, null, null, default(StoreLocation), "RunAs=App")]
        [InlineData("managedidentity", "UserA", null, null, null, default(StoreLocation), "RunAs=App;AppId=UserA")]
        [InlineData(null, null, "MyTenant", "OpenSesame", null, default(StoreLocation), null)]
        [InlineData(null, "UserB", null, "OpenSesame", null, default(StoreLocation), null)]
        [InlineData(null, "UserB", "MyTenant", null, null, default(StoreLocation), null)]
        [InlineData(null, "UserB", "MyTenant", "OpenSesame", null, default(StoreLocation), "RunAs=App;AppId=UserB;TenantId=MyTenant;AppKey=OpenSesame")]
        [InlineData(null, null, "MyOtherTenant", null, "MyThumbprint", StoreLocation.LocalMachine, null)]
        [InlineData(null, "UserC", null, null, "MyThumbprint", StoreLocation.LocalMachine, null)]
        [InlineData(null, "UserC", "MyOtherTenant", null, null, StoreLocation.LocalMachine, null)]
        [InlineData(null, "UserC", "MyOtherTenant", null, "MyThumbprint", StoreLocation.LocalMachine, "RunAs=App;AppId=UserC;TenantId=MyOtherTenant;CertificateThumbprint=MyThumbprint;CertificateStoreLocation=LocalMachine")]
        [InlineData(null, "UserD", "MyThirdTenant", null, "MyOtherThumbprint", StoreLocation.CurrentUser, "RunAs=App;AppId=UserD;TenantId=MyThirdTenant;CertificateThumbprint=MyOtherThumbprint;CertificateStoreLocation=CurrentUser")]
        public void GetConnectionString(string credential, string appId, string tenantId, string clientSecret, string certificate, StoreLocation certificateStoreLocation, string expected)
        {
            var options = new AzureIdentityOptions
            {
                Certificate = certificate,
                ClientId = appId,
                ClientCertificateStoreLocation = certificateStoreLocation,
                ClientSecret = clientSecret,
                Credential = credential,
                TenantId = tenantId,
            };

            Assert.Equal(expected, AppAuthenticationCredentialsFactory.GetConnectionString(options));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task NoConnection()
        {
            using var factory = new AppAuthenticationCredentialsFactory(NullLoggerFactory.Instance);
            Assert.Null(await factory.CreateAsync(new AzureIdentityOptions()));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CreateAsync()
        {
            const string token = "12345";
            const string connectionString = "RunAs=App";
            TimeSpan expiration = TimeSpan.FromHours(6);
            TimeSpan offset = TimeSpan.FromMinutes(10);
            TimeSpan delay = TimeSpan.FromMinutes(1);

            // Setup mocks
            AppAuthenticationResult result = CreateAppAuthenticationResult(token, DateTimeOffset.UtcNow.Add(expiration));
            var mock = new Mock<AzureServiceTokenProvider>(MockBehavior.Strict, connectionString, "https://login.microsoftonline.com/", (IHttpClientFactory)null);
            mock.Setup(
                p => p.GetAuthenticationResultAsync(
                    "https://storage.azure.com/",
                    true,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // Create the credentials factory with the mock token provider
            using var factory = new AppAuthenticationCredentialsFactory(
                s =>
                {
                    Assert.Equal(connectionString, s);
                    return mock.Object;
                },
                NullLoggerFactory.Instance);

            // Assert behavior through events
            int renewingCalls = 0, renewedCalls = 0;
            factory.Renewing += s =>
            {
                renewingCalls++;
                Assert.Equal(offset, s.RefreshOffset);
                Assert.Equal(delay, s.RefreshDelay);
                Assert.Null(s.Token);
                Assert.Same(mock.Object, s.TokenProvider);
            };

            factory.Renewed += n =>
            {
                renewedCalls++;
                Assert.Equal(token, n.Token);
                Assert.True(n.Frequency < expiration);
            };

            factory.RenewalFailed += (n, e) => throw new XunitException($"Renewal failed unexpectedly: {e}");

            StorageCredentials storageCredentials = await factory.CreateAsync(new AzureIdentityOptions { Credential = "managedidentity" }, offset, delay);
            Assert.True(storageCredentials.IsToken);
            Assert.Equal(1, renewingCalls);
            Assert.Equal(1, renewedCalls);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ConnectionFailure()
        {
            TimeSpan delay = TimeSpan.FromMinutes(1);

            // Setup mocks
            var mock = new Mock<AzureServiceTokenProvider>(MockBehavior.Strict, "RunAs=App", "https://login.microsoftonline.com/", (IHttpClientFactory)null);
            mock.Setup(
                p => p.GetAuthenticationResultAsync(
                    "https://storage.azure.com/",
                    true,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(CreateAzureServiceTokenProviderException());

            using var factory = new AppAuthenticationCredentialsFactory(s => mock.Object, NullLoggerFactory.Instance);

            // Assert behavior through events
            int renewingCalls = 0, failureCalls = 0;
            factory.Renewing += s => renewingCalls++;
            factory.Renewed += n => throw new XunitException("Renewal succeeded unexpectedly");
            factory.RenewalFailed += (n, e) =>
            {
                failureCalls++;
                Assert.Null(n.Token);
                Assert.Equal(delay, n.Frequency);
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => factory.CreateAsync(new AzureIdentityOptions { Credential = "managedidentity" }, TimeSpan.FromMinutes(10), delay));

            Assert.Equal(1, renewingCalls);
            Assert.Equal(1, failureCalls);
        }

        private static AppAuthenticationResult CreateAppAuthenticationResult(string token, DateTimeOffset expiresOn)
        {
            // This is a hack because none of the properties, methods, or ctors are public for AppAuthenticationResult!
            AppAuthenticationResult result = new AppAuthenticationResult();
            typeof(AppAuthenticationResult).GetProperty(nameof(AppAuthenticationResult.AccessToken)).GetSetMethod(nonPublic: true).Invoke(result, new object[] { token });
            typeof(AppAuthenticationResult).GetProperty(nameof(AppAuthenticationResult.ExpiresOn)).GetSetMethod(nonPublic: true).Invoke(result, new object[] { expiresOn });

            return result;
        }

        private static AzureServiceTokenProviderException CreateAzureServiceTokenProviderException()
        {
            // This is another hack because AzureServiceTokenProviderException doesn't provide any public ctor
            ConstructorInfo ctor = typeof(AzureServiceTokenProviderException).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(string), typeof(string), typeof(string), typeof(string) },
                null);

            return ctor.Invoke(new object[] { null, null, null, null }) as AzureServiceTokenProviderException;
        }
    }
}
