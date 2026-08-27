// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableActivityContextTests
    {
        private interface ITestService
        {
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetInput_InterfaceType_ThrowsActionableDeserializationError()
        {
            var context = new DurableActivityContext(
                CreateExtension(),
                "test-instance",
                "{}",
                "TestActivity");

            JsonSerializationException exception = Assert.Throws<JsonSerializationException>(
                () => ((IDurableActivityContext)context).GetInput<ITestService>());

            Assert.Contains("Failed to deserialize input for activity function 'TestActivity'", exception.Message);
            Assert.Contains($"into type '{typeof(ITestService).FullName}'", exception.Message);
            Assert.Contains("Activity inputs must be JSON-serializable values", exception.Message);
            Assert.Contains(
                "data transfer objects instead of interfaces or dependency-injected services",
                exception.Message);
            JsonSerializationException innerException =
                Assert.IsType<JsonSerializationException>(exception.InnerException);
            Assert.Contains($"The original error was: {innerException.Message}", exception.Message);
        }

        private static DurableTaskExtension CreateExtension()
        {
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };
            var wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var platformInformation = TestHelpers.GetMockPlatformInformationService();

            return new DurableTaskExtension(
                wrappedOptions,
                NullLoggerFactory.Instance,
                TestHelpers.GetTestNameResolver(),
                [
                    new AzureStorageDurabilityProviderFactory(
                        wrappedOptions,
                        new TestStorageServiceClientProviderFactory(),
                        TestHelpers.GetTestNameResolver(),
                        NullLoggerFactory.Instance,
                        platformInformation),
                ],
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: platformInformation);
        }
    }
}
