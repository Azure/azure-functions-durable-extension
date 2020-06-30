using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class AzureStorageDurabilityProviderFactoryTests
    {
        private readonly ITestOutputHelper output;

        public AzureStorageDurabilityProviderFactoryTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultWorkerId_IsMachineName()
        {
            var connectionStringResolver = new TestConnectionStringResolver();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(mockOptions, connectionStringResolver, nameResolver);

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal(Environment.MachineName, settings.WorkerId);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]

        public void EnvironmentIsVMSS_WorkerIdFromEnvironmentVariables()
        {
            var connectionStringResolver = new TestConnectionStringResolver();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "WEBSITE_CURRENT_STAMPNAME", "waws-prod-euapbn1-003" },
                { "RoleInstanceId", "dw0SmallDedicatedWebWorkerRole_hr0HostRole-3-VM-13" },
            });

            var factory = new AzureStorageDurabilityProviderFactory(mockOptions, connectionStringResolver, nameResolver);

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal("waws-prod-euapbn1-003:dw0SmallDedicatedWebWorkerRole_hr0HostRole-3-VM-13", settings.WorkerId);
        }
    }
}
