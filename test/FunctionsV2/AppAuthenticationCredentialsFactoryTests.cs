// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class AppAuthenticationCredentialsFactoryTests
    {
        [Theory]
        [InlineData(null, null, null, "RunAs=App")]
        [InlineData("UserA", null, null, "RunAs=App;AppId=UserA")]
        [InlineData("UserB", "MyTenant", "OpenSesame", "RunAs=App;AppId=UserB;TenantId=MyTenant;AppKey=OpenSesame")]
        public void GetAuthenticationConnectionString(string appId, string tenantId, string clientSecret, string expected)
        {
            var options = new ClientIdentityOptions
            {
                ClientId = appId,
                ClientSecret = clientSecret,
                TenantId = tenantId,
            };

            Assert.Equal(expected, AppAuthenticationCredentialsFactory.GetAuthenticationConnectionString(options));
        }
    }
}
