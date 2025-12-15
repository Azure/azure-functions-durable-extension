// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskOptionsFormatterTests
    {
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
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
            };

            // Act
            IOptionsFormatter formatter = options;
            string result = formatter.Format();
            JObject json = JObject.Parse(result);

            // Assert - verify complex properties are present
            Assert.NotNull(json);
            Assert.NotNull(json["HttpSettings"]);
            Assert.NotNull(json["Tracing"]);
            Assert.NotNull(json["Notifications"]);
            Assert.NotNull(json["AppLeaseOptions"]);
        }
    }
}
