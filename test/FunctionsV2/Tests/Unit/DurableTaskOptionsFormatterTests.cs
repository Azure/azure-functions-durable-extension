// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests for IOptionsFormatter implementation in DurableTaskOptions.
    /// The IOptionsFormatter interface is used by Azure WebJobs infrastructure to format
    /// configuration options for diagnostics and logging purposes.
    /// </summary>
    public class DurableTaskOptionsFormatterTests
    {
        private readonly ITestOutputHelper output;

        public DurableTaskOptionsFormatterTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Example demonstrating typical usage of the IOptionsFormatter.Format() method.
        /// In production, this is typically called by Azure WebJobs infrastructure for diagnostics.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Format_UsageExample()
        {
            // Arrange - Configure DurableTaskOptions as you would in your application
            var options = new DurableTaskOptions
            {
                HubName = "MyTaskHub",
                MaxConcurrentActivityFunctions = 20,
                MaxConcurrentOrchestratorFunctions = 10,
                ExtendedSessionsEnabled = true,
                UseAppLease = true,
            };

            // Act - Get formatted output (typically done by Azure WebJobs infrastructure)
            IOptionsFormatter formatter = options;
            string formattedOptions = formatter.Format();

            // This formatted string can be logged for diagnostics:
            // logger.LogInformation($"Current Durable Task Options: {formattedOptions}");

            // Output for demonstration
            this.output.WriteLine("Formatted DurableTaskOptions:");
            this.output.WriteLine(formattedOptions);

            // Assert - Verify the formatted output contains expected configuration
            JObject json = JObject.Parse(formattedOptions);
            Assert.Equal("MyTaskHub", json["HubName"].ToString());
            Assert.Equal(20, json["MaxConcurrentActivityFunctions"].Value<int?>());
            Assert.Equal(10, json["MaxConcurrentOrchestratorFunctions"].Value<int?>());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Format_ReturnsValidJson()
        {
            // Arrange
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                MaxConcurrentActivityFunctions = 10,
                MaxConcurrentOrchestratorFunctions = 5,
                ExtendedSessionsEnabled = true,
            };

            // Act
            IOptionsFormatter formatter = options;
            string result = formatter.Format();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);

            // Verify it's valid JSON
            JObject json = JObject.Parse(result);
            Assert.NotNull(json);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Format_IncludesExpectedProperties()
        {
            // Arrange
            var options = new DurableTaskOptions
            {
                HubName = "MyTestHub",
                MaxConcurrentActivityFunctions = 15,
                MaxConcurrentOrchestratorFunctions = 7,
                ExtendedSessionsEnabled = true,
                ExtendedSessionIdleTimeoutInSeconds = 60,
                UseAppLease = false,
            };

            // Act
            IOptionsFormatter formatter = options;
            string result = formatter.Format();
            JObject json = JObject.Parse(result);

            // Assert
            Assert.Equal("MyTestHub", json["HubName"].ToString());
            Assert.Equal(15, json["MaxConcurrentActivityFunctions"].Value<int?>());
            Assert.Equal(7, json["MaxConcurrentOrchestratorFunctions"].Value<int?>());
            Assert.True(json["ExtendedSessionsEnabled"].Value<bool>());
            Assert.Equal(60, json["ExtendedSessionIdleTimeoutInSeconds"].Value<int>());
            Assert.False(json["UseAppLease"].Value<bool>());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Format_HandlesNullValues()
        {
            // Arrange
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                MaxConcurrentActivityFunctions = null,
                DefaultVersion = null,
            };

            // Act
            IOptionsFormatter formatter = options;
            string result = formatter.Format();
            JObject json = JObject.Parse(result);

            // Assert
            Assert.NotNull(json);
            Assert.Null(json["MaxConcurrentActivityFunctions"].Value<int?>());
            Assert.Null(json["DefaultVersion"].Value<string>());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Format_IncludesEnumValues()
        {
            // Arrange
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                OverridableExistingInstanceStates = OverridableStates.AnyState,
            };

            // Act
            IOptionsFormatter formatter = options;
            string result = formatter.Format();
            JObject json = JObject.Parse(result);

            // Assert
            Assert.NotNull(json);
            Assert.Equal("AnyState", json["OverridableExistingInstanceStates"].ToString());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Format_IncludesComplexProperties()
        {
            // Arrange
            // Complex properties (HttpSettings, Tracing, Notifications, AppLeaseOptions)
            // are initialized with default values in DurableTaskOptions constructor
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
            };

            // Act
            IOptionsFormatter formatter = options;
            string result = formatter.Format();
            JObject json = JObject.Parse(result);

            // Assert - verify complex properties with default values are present
            Assert.NotNull(json);
            Assert.NotNull(json["HttpSettings"]);
            Assert.NotNull(json["Tracing"]);
            Assert.NotNull(json["Notifications"]);
            Assert.NotNull(json["AppLeaseOptions"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Format_ExcludesStorageProviderForSecurity()
        {
            // Arrange
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
            };
            options.StorageProvider["connectionString"] = "sensitive-value";

            // Act
            IOptionsFormatter formatter = options;
            string result = formatter.Format();
            JObject json = JObject.Parse(result);

            // Assert - StorageProvider should not be included to avoid exposing secrets
            Assert.NotNull(json);
            Assert.Null(json["StorageProvider"]);
        }
    }
}
