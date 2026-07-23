// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class AzureStorageOptionsTests : IDisposable
    {
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("WEBSITE_SKU", null);
        }

#if !FUNCTIONS_V1
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void MaxQueuePollingInterval_NonFlexConsumption_DefaultValue()
        {
            Environment.SetEnvironmentVariable("WEBSITE_SKU", "Free");

            var options = new AzureStorageOptions();
            Assert.Equal(TimeSpan.FromSeconds(30), options.MaxQueuePollingInterval);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void MaxQueuePollingInterval_NonFlexConsumption_SetCustomValue()
        {
            Environment.SetEnvironmentVariable("WEBSITE_SKU", "Free");

            var options = new AzureStorageOptions();
            options.MaxQueuePollingInterval = TimeSpan.FromSeconds(4);
            Assert.Equal(TimeSpan.FromSeconds(4), options.MaxQueuePollingInterval);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void MaxQueuePollingInterval_FlexConsumption_DefaultValue()
        {
            Environment.SetEnvironmentVariable("WEBSITE_SKU", "FlexConsumption");

            var options = new AzureStorageOptions();
            Assert.Equal(TimeSpan.FromSeconds(1), options.MaxQueuePollingInterval);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void MaxQueuePollingInterval_FlexConsumption_SetCustomValue()
        {
            Environment.SetEnvironmentVariable("WEBSITE_SKU", "FlexConsumption");

            var options = new AzureStorageOptions();
            options.MaxQueuePollingInterval = TimeSpan.FromSeconds(6);
            Assert.Equal(TimeSpan.FromSeconds(6), options.MaxQueuePollingInterval);
        }

        // Reproduces https://github.com/Azure/azure-functions-durable-extension/issues/3094: when an app
        // explicitly sets useLegacyPartitionManagement=true, the now-default table partition manager must be
        // disabled so the storage provider is never given both settings (which throws "Cannot use both ...").
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Validate_LegacyPartitionManagementEnabled_DisablesDefaultTablePartitionManagement()
        {
            var options = new AzureStorageOptions
            {
                UseLegacyPartitionManagement = true,
            };

            // Table partition management defaults to true, so both settings are enabled before validation.
            Assert.True(options.UseTablePartitionManagement);

            options.Validate(NullLogger.Instance);

            // The explicit legacy opt-in wins; the defaulted table partition manager is turned off.
            Assert.True(options.UseLegacyPartitionManagement);
            Assert.False(options.UseTablePartitionManagement);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Validate_LegacyPartitionManagementDisabled_KeepsDefaultTablePartitionManagement()
        {
            var options = new AzureStorageOptions();

            Assert.False(options.UseLegacyPartitionManagement);
            Assert.True(options.UseTablePartitionManagement);

            options.Validate(NullLogger.Instance);

            // With no conflicting legacy opt-in, the default table partition manager is preserved.
            Assert.False(options.UseLegacyPartitionManagement);
            Assert.True(options.UseTablePartitionManagement);
        }
#endif
    }
}
