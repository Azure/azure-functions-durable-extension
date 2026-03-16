// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class AzureStorageOptionsTests
    {
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
#endif
    }
}
