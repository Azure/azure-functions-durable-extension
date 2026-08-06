// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TelemetryActivatorTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetAzureTokenCredential_ReturnsCredentialFromHostConfiguration()
        {
            using TelemetryConfiguration hostConfiguration = TelemetryConfiguration.CreateDefault();
            var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
            hostConfiguration.SetAzureTokenCredential(credential);

            MethodInfo method = typeof(TelemetryActivator).GetMethod(
                "GetAzureTokenCredential",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            Assert.Same(credential, method.Invoke(obj: null, new object[] { hostConfiguration }));
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
    }
}
