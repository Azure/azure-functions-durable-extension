// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.Core.Entities.OperationFormat;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests for ProtobufUtils conversion methods.
    /// </summary>
    public class ProtobufUtilsTests
    {
        /// <summary>
        /// Tests that ToOperationAction applies the default version when the protobuf message has no version specified.
        /// were not receiving the host's default version.
        /// </summary>
        [Theory]
        [InlineData(null, "2025-10-23", "2025-10-23")] // Null version in proto, should use default
        [InlineData("", "2025-10-23", "2025-10-23")] // Empty version in proto, should use default
        [InlineData("1.0.0", "2025-10-23", "1.0.0")] // Explicit version in proto, should preserve it
        [InlineData("v2.0", null, "v2.0")] // Explicit version, null default, should preserve it
        [InlineData(null, null, null)] // Both null, should remain null
        [InlineData("", null, null)] // Empty version, null default, should be null
        [InlineData(null, "", "")] // Null version, empty default, should be empty
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToOperationAction_StartNewOrchestration_AppliesDefaultVersion(
            string protoVersion,
            string defaultVersion,
            string expectedVersion)
        {
            // Arrange
            var startNewOrchestrationAction = new P.StartNewOrchestrationAction
            {
                Name = "TestOrchestrator",
                InstanceId = "test-instance-id",
                Input = "\"test-input\"",
            };

            // Only set Version if it's not null (protobuf treats null and not-set differently)
            if (protoVersion != null)
            {
                startNewOrchestrationAction.Version = protoVersion;
            }

            var operationAction = new P.OperationAction
            {
                StartNewOrchestration = startNewOrchestrationAction,
            };

            // Act
            var result = operationAction.ToOperationAction(defaultVersion);

            // Assert
            Assert.NotNull(result);
            var startOrchestrationResult = Assert.IsType<StartNewOrchestrationOperationAction>(result);
            Assert.Equal("TestOrchestrator", startOrchestrationResult.Name);
            Assert.Equal("test-instance-id", startOrchestrationResult.InstanceId);
            Assert.Equal(expectedVersion, startOrchestrationResult.Version);
        }

        /// <summary>
        /// Tests that ToEntityBatchResult properly passes the default version to all actions.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToEntityBatchResult_PassesDefaultVersionToActions()
        {
            // Arrange
            var startNewOrchestrationAction = new P.StartNewOrchestrationAction
            {
                Name = "TestOrchestrator",
                InstanceId = "test-instance-id",
            };

            var operationAction = new P.OperationAction
            {
                StartNewOrchestration = startNewOrchestrationAction,
            };

            var entityBatchResult = new P.EntityBatchResult
            {
                EntityState = "{}",
            };
            entityBatchResult.Actions.Add(operationAction);
            entityBatchResult.Results.Add(new P.OperationResult
            {
                Success = new P.OperationResultSuccess
                {
                    Result = "null",
                },
            });

            var defaultVersion = "2025-10-23";

            // Act
            var result = entityBatchResult.ToEntityBatchResult(defaultVersion);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Actions);
            var action = Assert.IsType<StartNewOrchestrationOperationAction>(result.Actions[0]);
            Assert.Equal(defaultVersion, action.Version);
        }

        /// <summary>
        /// Tests that when no default version is provided, behavior should match previous implementation.
        /// </summary>
        [Theory]
        [InlineData(null, null)] // Null version, no default
        [InlineData("", null)] // Empty version becomes null when no default
        [InlineData("v1.0", "v1.0")] // Explicit version preserved
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToOperationAction_NoDefaultVersion(
            string protoVersion,
            string expectedVersion)
        {
            // Arrange
            var startNewOrchestrationAction = new P.StartNewOrchestrationAction
            {
                Name = "TestOrchestrator",
                InstanceId = "test-instance-id",
            };

            if (protoVersion != null)
            {
                startNewOrchestrationAction.Version = protoVersion;
            }

            var operationAction = new P.OperationAction
            {
                StartNewOrchestration = startNewOrchestrationAction,
            };

            // Act - Call without default version (backward compatible)
            var result = operationAction.ToOperationAction();

            // Assert
            var startOrchestrationResult = Assert.IsType<StartNewOrchestrationOperationAction>(result);
            Assert.Equal(expectedVersion, startOrchestrationResult.Version);
        }

        /// <summary>
        /// Tests that ToEntityBatchResult with multiple StartNewOrchestration actions applies defaultVersion to all of them.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToEntityBatchResult_MultipleStartOrchestrationActions_AllReceiveDefaultVersion()
        {
            // Arrange
            var entityBatchResult = new P.EntityBatchResult
            {
                EntityState = "{}",
            };

            // Add multiple orchestration actions with different version configurations
            entityBatchResult.Actions.Add(new P.OperationAction
            {
                StartNewOrchestration = new P.StartNewOrchestrationAction
                {
                    Name = "Orchestrator1",
                    InstanceId = "instance-1",
                },
            });

            // Add action with empty version - should use default
            entityBatchResult.Actions.Add(new P.OperationAction
            {
                StartNewOrchestration = new P.StartNewOrchestrationAction
                {
                    Name = "Orchestrator2",
                    InstanceId = "instance-2",
                    Version = "", // Empty version - should use default
                },
            });

            entityBatchResult.Actions.Add(new P.OperationAction
            {
                StartNewOrchestration = new P.StartNewOrchestrationAction
                {
                    Name = "Orchestrator3",
                    InstanceId = "instance-3",
                    Version = "explicit-v1", // Explicit version - should preserve
                },
            });

            // Add corresponding results
            for (int i = 0; i < 3; i++)
            {
                entityBatchResult.Results.Add(new P.OperationResult
                {
                    Success = new P.OperationResultSuccess { Result = "null" },
                });
            }

            var defaultVersion = "host-default-v2.0";

            // Act
            var result = entityBatchResult.ToEntityBatchResult(defaultVersion);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Actions.Count);

            // First action - no version set, should use default
            var action1 = Assert.IsType<StartNewOrchestrationOperationAction>(result.Actions[0]);
            Assert.Equal("Orchestrator1", action1.Name);
            Assert.Equal(defaultVersion, action1.Version);

            // Second action - empty version, should use default
            var action2 = Assert.IsType<StartNewOrchestrationOperationAction>(result.Actions[1]);
            Assert.Equal("Orchestrator2", action2.Name);
            Assert.Equal(defaultVersion, action2.Version);

            // Third action - explicit version, should preserve it
            var action3 = Assert.IsType<StartNewOrchestrationOperationAction>(result.Actions[2]);
            Assert.Equal("Orchestrator3", action3.Name);
            Assert.Equal("explicit-v1", action3.Version);
        }

        /// <summary>
        /// Tests that ToEntityBatchResult with mixed action types only affects StartNewOrchestration actions.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToEntityBatchResult_MixedActionTypes_OnlyStartOrchestrationAffected()
        {
            // Arrange
            var entityBatchResult = new P.EntityBatchResult
            {
                EntityState = "{}",
            };

            // Add StartNewOrchestration action
            entityBatchResult.Actions.Add(new P.OperationAction
            {
                StartNewOrchestration = new P.StartNewOrchestrationAction
                {
                    Name = "TestOrchestrator",
                    InstanceId = "orch-instance",
                },
            });

            // Add SendSignal action
            entityBatchResult.Actions.Add(new P.OperationAction
            {
                SendSignal = new P.SendSignalAction
                {
                    Name = "TestSignal",
                    InstanceId = "@entity@test",
                    Input = "{}",
                },
            });

            // Add corresponding results
            entityBatchResult.Results.Add(new P.OperationResult
            {
                Success = new P.OperationResultSuccess { Result = "null" },
            });
            entityBatchResult.Results.Add(new P.OperationResult
            {
                Success = new P.OperationResultSuccess { Result = "null" },
            });

            var defaultVersion = "default-v1.0";

            // Act
            var result = entityBatchResult.ToEntityBatchResult(defaultVersion);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Actions.Count);

            // StartNewOrchestration should have default version applied
            var orchAction = Assert.IsType<StartNewOrchestrationOperationAction>(result.Actions[0]);
            Assert.Equal(defaultVersion, orchAction.Version);

            // SendSignal should not be affected
            var signalAction = Assert.IsType<SendSignalOperationAction>(result.Actions[1]);
            Assert.Equal("TestSignal", signalAction.Name);
        }

        /// <summary>
        /// Tests that null EntityBatchResult input returns null output (defensive programming).
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToEntityBatchResult_NullInput_ReturnsNull()
        {
            // Arrange
            P.EntityBatchResult nullBatchResult = null;

            // Act
            var result = nullBatchResult.ToEntityBatchResult("default-version");

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that null OperationAction input returns null output (defensive programming).
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToOperationAction_NullInput_ReturnsNull()
        {
            // Arrange
            P.OperationAction nullAction = null;

            // Act
            var result = nullAction.ToOperationAction("default-version");

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that StartNewOrchestration with scheduled start time preserves the schedule while applying version.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToOperationAction_StartNewOrchestrationWithSchedule_PreservesScheduleAndAppliesVersion()
        {
            // Arrange
            var scheduledTime = DateTime.UtcNow.AddMinutes(30);
            var startNewOrchestrationAction = new P.StartNewOrchestrationAction
            {
                Name = "ScheduledOrchestrator",
                InstanceId = "scheduled-instance",
                ScheduledTime = Timestamp.FromDateTime(scheduledTime),
            };

            // No version - should use default

            var operationAction = new P.OperationAction
            {
                StartNewOrchestration = startNewOrchestrationAction,
            };

            var defaultVersion = "scheduled-default-v1";

            // Act
            var result = operationAction.ToOperationAction(defaultVersion);

            // Assert
            Assert.NotNull(result);
            var startOrchestrationResult = Assert.IsType<StartNewOrchestrationOperationAction>(result);
            Assert.Equal("ScheduledOrchestrator", startOrchestrationResult.Name);
            Assert.Equal(defaultVersion, startOrchestrationResult.Version);
            Assert.NotNull(startOrchestrationResult.ScheduledStartTime);
            Assert.Equal(scheduledTime, startOrchestrationResult.ScheduledStartTime.Value, TimeSpan.FromSeconds(1));
        }
    }
}
