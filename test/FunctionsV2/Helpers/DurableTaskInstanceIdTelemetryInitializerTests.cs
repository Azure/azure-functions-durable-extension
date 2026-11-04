// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests for <see cref="DurableTaskInstanceIdTelemetryInitializer"/> which populates
    /// Application Insights Operation.Name and optionally appends the orchestration instance ID.
    /// </summary>
    public class DurableTaskInstanceIdTelemetryInitializerTests
    {
        /// <summary>
        /// Verifies that when IncludeInstanceIdInOperationName is enabled,
        /// the instance ID is appended to orchestration spans' Operation.Name.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithIncludeInstanceIdTrue_AppendsInstanceId()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new DependencyTelemetry();
            telemetry.Context.Operation.Name = "orchestration:Function1";

            using var activity = new Activity("orchestration:Function1");
            activity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
            activity.SetTag(Schema.Task.InstanceId, "test-instance-id");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("orchestration:Function1 (test-instance-id)", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }

        /// <summary>
        /// Verifies that when IncludeInstanceIdInOperationName is disabled,
        /// the Operation.Name remains unchanged even when instance ID is available.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithIncludeInstanceIdFalse_DoesNotAppendInstanceId()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(false);
            var telemetry = new DependencyTelemetry();
            telemetry.Context.Operation.Name = "orchestration:Function1";

            using var activity = new Activity("orchestration:Function1");
            activity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
            activity.SetTag(Schema.Task.InstanceId, "test-instance-id");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("orchestration:Function1", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }

        /// <summary>
        /// Verifies that disabling instance ID suffixes does not leave the operation name empty.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithIncludeInstanceIdFalseAndNoCurrentActivity_SetsFromTelemetryName()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(false);
            var telemetry = new DependencyTelemetry
            {
                Name = "orchestration:Function1",
            };

            // Act
            initializer.Initialize(telemetry);

            // Assert
            Assert.Equal("orchestration:Function1", telemetry.Context.Operation.Name);
        }

        /// <summary>
        /// Verifies that create_orchestration spans are excluded from instance ID appending.
        /// The instance ID should only appear on the orchestration execution span, not the
        /// span that represents the client scheduling the orchestration.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithCreateOrchestration_DoesNotAppendInstanceId()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new DependencyTelemetry();
            telemetry.Context.Operation.Name = "create_orchestration:Function1";

            using var activity = new Activity("create_orchestration:Function1");
            activity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
            activity.SetTag(Schema.Task.Operation, TraceActivityConstants.CreateOrchestration);
            activity.SetTag(Schema.Task.InstanceId, "test-instance-id");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("create_orchestration:Function1", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }

        /// <summary>
        /// Verifies that when Operation.Name is empty (which can happen in the Failures tab),
        /// the initializer first sets it from the telemetry name before appending the instance ID.
        /// This fixes the issue where failures show an empty operation name.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithEmptyOperationName_SetsFromTelemetryNameAndAppends()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new DependencyTelemetry
            {
                Name = "orchestration:Function1",
            };

            using var activity = new Activity("orchestration:Function1");
            activity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
            activity.SetTag(Schema.Task.InstanceId, "test-instance-id");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("orchestration:Function1 (test-instance-id)", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }

        /// <summary>
        /// Verifies that telemetry without its own Name falls back to the current Activity display name.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithNonOperationTelemetryAndEmptyOperationName_SetsFromActivityAndAppends()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new ExceptionTelemetry();

            using var activity = new Activity("orchestration:Function1");
            activity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
            activity.SetTag(Schema.Task.InstanceId, "test-instance-id");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("orchestration:Function1 (test-instance-id)", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }

        /// <summary>
        /// Verifies that activity function spans (not to be confused with System.Diagnostics.Activity)
        /// also have the instance ID appended when the feature is enabled.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithActivitySpan_AppendsInstanceId()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new DependencyTelemetry();
            telemetry.Context.Operation.Name = "activity:SayHello";

            using var activity = new Activity("activity:SayHello");
            activity.SetTag(Schema.Task.Type, TraceActivityConstants.Activity);
            activity.SetTag(Schema.Task.InstanceId, "test-instance-id");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("activity:SayHello (test-instance-id)", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }

        /// <summary>
        /// Verifies that durable entity spans have the instance ID appended when the feature is enabled.
        /// Entity instance IDs typically include the entity name and key (e.g., "@Counter@myCounter").
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithEntitySpan_AppendsInstanceId()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new DependencyTelemetry();
            telemetry.Context.Operation.Name = "entity:Counter:add";

            using var activity = new Activity("entity:Counter:add");
            activity.SetTag(Schema.Task.Type, TraceActivityConstants.Entity);
            activity.SetTag(Schema.Task.InstanceId, "@Counter@myCounter");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("entity:Counter:add (@Counter@myCounter)", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }

        /// <summary>
        /// Verifies that spans with unknown or unsupported types are not modified.
        /// Only orchestration, activity, and entity spans should have instance IDs appended.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_WithUnknownSpanType_DoesNotModifyOperationName()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new DependencyTelemetry();
            telemetry.Context.Operation.Name = "unknown:Function1";

            using var activity = new Activity("unknown:Function1");
            activity.SetTag(Schema.Task.Type, "unknown");
            activity.SetTag(Schema.Task.InstanceId, "test-instance-id");
            activity.Start();

            try
            {
                // Act
                initializer.Initialize(telemetry);

                // Assert
                Assert.Equal("unknown:Function1", telemetry.Context.Operation.Name);
            }
            finally
            {
                activity.Stop();
            }
        }
    }
}
