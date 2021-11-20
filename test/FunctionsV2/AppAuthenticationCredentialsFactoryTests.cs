// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class AppAuthenticationCredentialsFactoryTests
    {
        [Theory]
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
    }
}
