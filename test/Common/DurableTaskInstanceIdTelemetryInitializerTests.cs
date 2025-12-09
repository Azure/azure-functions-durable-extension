// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskInstanceIdTelemetryInitializerTests
    {
        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void Initialize_WithEmptyOperationName_SetsFromActivityAndAppends()
        {
            // Arrange
            var initializer = new DurableTaskInstanceIdTelemetryInitializer(true);
            var telemetry = new DependencyTelemetry();
            telemetry.Context.Operation.Name = null; // Empty

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
    }
}
