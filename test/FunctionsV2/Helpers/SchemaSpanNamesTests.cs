// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Unit tests for the Schema.SpanNames class.
    /// </summary>
    public class SchemaSpanNamesTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateOrchestration_WithoutInstanceId_ReturnsExpectedFormat()
        {
            // Arrange
            string name = "MyOrchestration";
            string version = null;

            // Act
            string result = Schema.SpanNames.CreateOrchestration(name, version);

            // Assert
            Assert.Equal($"{TraceActivityConstants.CreateOrchestration}:{name}", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateOrchestration_WithVersion_ReturnsExpectedFormat()
        {
            // Arrange
            string name = "MyOrchestration";
            string version = "1.0";

            // Act
            string result = Schema.SpanNames.CreateOrchestration(name, version);

            // Assert
            Assert.Equal($"{TraceActivityConstants.CreateOrchestration}:{name}@{version}", result);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, "test-instance-123")]
        [InlineData(true, "abc-def-ghi")]
        public void CreateOrchestration_WithIncludeInstanceIdTrue_IncludesInstanceId(bool includeInstanceId, string instanceId)
        {
            // Arrange
            string name = "MyOrchestration";
            string version = null;

            // Act
            string result = Schema.SpanNames.CreateOrchestration(name, version, instanceId, includeInstanceId);

            // Assert
            Assert.Equal($"{TraceActivityConstants.CreateOrchestration}:{name} ({instanceId})", result);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false, "test-instance-123")]
        [InlineData(false, "abc-def-ghi")]
        public void CreateOrchestration_WithIncludeInstanceIdFalse_ExcludesInstanceId(bool includeInstanceId, string instanceId)
        {
            // Arrange
            string name = "MyOrchestration";
            string version = null;

            // Act
            string result = Schema.SpanNames.CreateOrchestration(name, version, instanceId, includeInstanceId);

            // Assert
            Assert.Equal($"{TraceActivityConstants.CreateOrchestration}:{name}", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateOrchestration_WithNullInstanceId_ExcludesInstanceId()
        {
            // Arrange
            string name = "MyOrchestration";
            string version = null;
            string instanceId = null;

            // Act
            string result = Schema.SpanNames.CreateOrchestration(name, version, instanceId, includeInstanceId: true);

            // Assert
            Assert.Equal($"{TraceActivityConstants.CreateOrchestration}:{name}", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateOrchestration_WithEmptyInstanceId_ExcludesInstanceId()
        {
            // Arrange
            string name = "MyOrchestration";
            string version = null;
            string instanceId = string.Empty;

            // Act
            string result = Schema.SpanNames.CreateOrchestration(name, version, instanceId, includeInstanceId: true);

            // Assert
            Assert.Equal($"{TraceActivityConstants.CreateOrchestration}:{name}", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateOrchestration_WithVersionAndInstanceId_ReturnsExpectedFormat()
        {
            // Arrange
            string name = "MyOrchestration";
            string version = "1.0";
            string instanceId = "test-instance-123";

            // Act
            string result = Schema.SpanNames.CreateOrchestration(name, version, instanceId, includeInstanceId: true);

            // Assert
            Assert.Equal($"{TraceActivityConstants.CreateOrchestration}:{name}@{version} ({instanceId})", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CallOrSignalEntity_WithoutInstanceId_ReturnsExpectedFormat()
        {
            // Arrange
            string name = "Counter";
            string operation = "Add";

            // Act
            string result = Schema.SpanNames.CallOrSignalEntity(name, operation);

            // Assert
            Assert.Equal($"{TraceActivityConstants.Entity}:{name}:{operation}", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CallOrSignalEntity_WithIncludeInstanceIdTrue_IncludesInstanceId()
        {
            // Arrange
            string name = "Counter";
            string operation = "Add";
            string instanceId = "entity-instance-123";

            // Act
            string result = Schema.SpanNames.CallOrSignalEntity(name, operation, instanceId, includeInstanceId: true);

            // Assert
            Assert.Equal($"{TraceActivityConstants.Entity}:{name}:{operation} ({instanceId})", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CallOrSignalEntity_WithIncludeInstanceIdFalse_ExcludesInstanceId()
        {
            // Arrange
            string name = "Counter";
            string operation = "Add";
            string instanceId = "entity-instance-123";

            // Act
            string result = Schema.SpanNames.CallOrSignalEntity(name, operation, instanceId, includeInstanceId: false);

            // Assert
            Assert.Equal($"{TraceActivityConstants.Entity}:{name}:{operation}", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EntityStartsAnOrchestration_WithoutInstanceId_ReturnsExpectedFormat()
        {
            // Arrange
            string name = "Counter";

            // Act
            string result = Schema.SpanNames.EntityStartsAnOrchestration(name);

            // Assert
            Assert.Equal($"{name}:{TraceActivityConstants.CreateOrchestration}", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EntityStartsAnOrchestration_WithIncludeInstanceIdTrue_IncludesInstanceId()
        {
            // Arrange
            string name = "Counter";
            string instanceId = "orch-instance-123";

            // Act
            string result = Schema.SpanNames.EntityStartsAnOrchestration(name, instanceId, includeInstanceId: true);

            // Assert
            Assert.Equal($"{name}:{TraceActivityConstants.CreateOrchestration} ({instanceId})", result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EntityStartsAnOrchestration_WithIncludeInstanceIdFalse_ExcludesInstanceId()
        {
            // Arrange
            string name = "Counter";
            string instanceId = "orch-instance-123";

            // Act
            string result = Schema.SpanNames.EntityStartsAnOrchestration(name, instanceId, includeInstanceId: false);

            // Assert
            Assert.Equal($"{name}:{TraceActivityConstants.CreateOrchestration}", result);
        }
    }
}
