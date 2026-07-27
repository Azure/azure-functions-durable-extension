// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Buffers;
using System.Linq;
using DurableTask.Core.Entities.OperationFormat;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using CoreOrchestrationStatus = global::DurableTask.Core.OrchestrationStatus;
using CorePurgeResult = global::DurableTask.Core.PurgeResult;
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

        [Theory]
        [InlineData(32)]
        [InlineData(1024 * 1024)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Base64Decode_OrchestratorResponse_RoundTripsAndReturnsBuffer(int customStatusLength)
        {
            var expected = new P.OrchestratorResponse
            {
                InstanceId = "test-instance",
                CustomStatus = new string('x', customStatusLength),
                RequiresHistory = true,
            };
            expected.Actions.Add(new P.OrchestratorAction
            {
                Id = 42,
                CompleteOrchestration = new P.CompleteOrchestrationAction
                {
                    Result = "\"done\"",
                    FailureDetails = new P.TaskFailureDetails
                    {
                        ErrorType = "TestError",
                        ErrorMessage = "Test failure details",
                    },
                },
            });
            string encoded = Convert.ToBase64String(expected.ToByteArray());
            var pool = new TrackingByteArrayPool();

            P.OrchestratorResponse actual =
                ProtobufUtils.Base64Decode(encoded, P.OrchestratorResponse.Parser, pool);

            Assert.Equal(expected, actual);
            AssertBufferReturned(pool, encoded);
        }

        [Theory]
        [InlineData(32)]
        [InlineData(1024 * 1024)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Base64Decode_EntityBatchResult_RoundTripsAndReturnsBuffer(int entityStateLength)
        {
            var expected = new P.EntityBatchResult
            {
                EntityState = new string('x', entityStateLength),
                CompletionToken = "test-token",
                FailureDetails = new P.TaskFailureDetails
                {
                    ErrorType = "TestError",
                    ErrorMessage = "Test failure details",
                },
                RequiresState = true,
            };
            expected.Results.Add(new P.OperationResult
            {
                Success = new P.OperationResultSuccess
                {
                    Result = "\"done\"",
                },
            });
            string encoded = Convert.ToBase64String(expected.ToByteArray());
            var pool = new TrackingByteArrayPool();

            P.EntityBatchResult actual =
                ProtobufUtils.Base64Decode(encoded, P.EntityBatchResult.Parser, pool);

            Assert.Equal(expected, actual);
            AssertBufferReturned(pool, encoded);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Base64Decode_MalformedBase64_ThrowsAndReturnsBuffer(bool orchestratorResponse)
        {
            FormatException expectedException =
                Assert.Throws<FormatException>(() => Convert.FromBase64String("not-base64"));
            var pool = new TrackingByteArrayPool();
            FormatException actualException;

            if (orchestratorResponse)
            {
                actualException = Assert.Throws<FormatException>(
                    () => ProtobufUtils.Base64Decode("not-base64", P.OrchestratorResponse.Parser, pool));
            }
            else
            {
                actualException = Assert.Throws<FormatException>(
                    () => ProtobufUtils.Base64Decode("not-base64", P.EntityBatchResult.Parser, pool));
            }

            Assert.Equal(expectedException.Message, actualException.Message);
            AssertBufferReturned(pool, "not-base64");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Base64Decode_MalformedProtobuf_ThrowsAndReturnsBuffer(bool orchestratorResponse)
        {
            string encoded = Convert.ToBase64String([0x80]);
            var pool = new TrackingByteArrayPool();
            InvalidProtocolBufferException expectedException;
            InvalidProtocolBufferException actualException;

            if (orchestratorResponse)
            {
                expectedException = Assert.Throws<InvalidProtocolBufferException>(
                    () => P.OrchestratorResponse.Parser.ParseFrom(Convert.FromBase64String(encoded)));
                actualException = Assert.Throws<InvalidProtocolBufferException>(
                    () => ProtobufUtils.Base64Decode(encoded, P.OrchestratorResponse.Parser, pool));
            }
            else
            {
                expectedException = Assert.Throws<InvalidProtocolBufferException>(
                    () => P.EntityBatchResult.Parser.ParseFrom(Convert.FromBase64String(encoded)));
                actualException = Assert.Throws<InvalidProtocolBufferException>(
                    () => ProtobufUtils.Base64Decode(encoded, P.EntityBatchResult.Parser, pool));
            }

            Assert.Equal(expectedException.Message, actualException.Message);
            AssertBufferReturned(pool, encoded);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Base64Decode_WhitespaceOnly_DoesNotOverRent(bool orchestratorResponse)
        {
            string encoded = $"\t\r\n{new string(' ', 1024 * 1024)}";
            var pool = new TrackingByteArrayPool();

            IMessage actual = orchestratorResponse
                ? ProtobufUtils.Base64Decode(encoded, P.OrchestratorResponse.Parser, pool)
                : ProtobufUtils.Base64Decode(encoded, P.EntityBatchResult.Parser, pool);

            Assert.Equal(0, actual.CalculateSize());
            AssertBufferReturned(pool, encoded);
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

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreatePurgeInstancesResponse_IsCompleteTrue_MapsCorrectly()
        {
            // Arrange
            var result = new CorePurgeResult(10, isComplete: true);

            // Act
            var response = ProtobufUtils.CreatePurgeInstancesResponse(result);

            // Assert
            Assert.Equal(10, response.DeletedInstanceCount);
            Assert.True(response.IsComplete);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreatePurgeInstancesResponse_IsCompleteFalse_MapsCorrectly()
        {
            // Arrange
            var result = new CorePurgeResult(5, isComplete: false);

            // Act
            var response = ProtobufUtils.CreatePurgeInstancesResponse(result);

            // Assert
            Assert.Equal(5, response.DeletedInstanceCount);
            Assert.False(response.IsComplete);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreatePurgeInstancesResponse_IsCompleteNull_LeavesUnset()
        {
            // Arrange
            var result = new CorePurgeResult(3);

            // Act
            var response = ProtobufUtils.CreatePurgeInstancesResponse(result);

            // Assert
            Assert.Equal(3, response.DeletedInstanceCount);
            Assert.Null(response.IsComplete);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToPurgeInstanceFilter_WithoutTimeout_BasicFieldsMapped()
        {
            // Arrange
            var createdFrom = DateTime.UtcNow.AddDays(-1);
            var request = new P.PurgeInstancesRequest
            {
                PurgeInstanceFilter = new P.PurgeInstanceFilter
                {
                    CreatedTimeFrom = Timestamp.FromDateTime(createdFrom),
                },
            };

            // Act
            var filter = ProtobufUtils.ToPurgeInstanceFilter(request);

            // Assert
            Assert.Equal(createdFrom, filter.CreatedTimeFrom, TimeSpan.FromSeconds(1));
            Assert.Null(filter.CreatedTimeTo);
            Assert.Null(filter.Timeout);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToPurgeInstanceFilter_WithTimeout_MapsTimeout()
        {
            // Arrange
            var timeout = TimeSpan.FromSeconds(25);
            var request = new P.PurgeInstancesRequest
            {
                PurgeInstanceFilter = new P.PurgeInstanceFilter
                {
                    CreatedTimeFrom = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    Timeout = Duration.FromTimeSpan(timeout),
                },
            };

            // Act
            var filter = ProtobufUtils.ToPurgeInstanceFilter(request);

            // Assert
            Assert.Equal(timeout, filter.Timeout);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToPurgeInstanceFilter_NegativeTimeout_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var request = new P.PurgeInstancesRequest
            {
                PurgeInstanceFilter = new P.PurgeInstanceFilter
                {
                    CreatedTimeFrom = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    Timeout = new Duration { Seconds = -1 },
                },
            };

            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => ProtobufUtils.ToPurgeInstanceFilter(request));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToPurgeInstanceFilter_WithStatusFilter_MapsStatuses()
        {
            // Arrange
            var request = new P.PurgeInstancesRequest
            {
                PurgeInstanceFilter = new P.PurgeInstanceFilter
                {
                    CreatedTimeFrom = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                },
            };
            request.PurgeInstanceFilter.RuntimeStatus.Add((P.OrchestrationStatus)(int)CoreOrchestrationStatus.Completed);
            request.PurgeInstanceFilter.RuntimeStatus.Add((P.OrchestrationStatus)(int)CoreOrchestrationStatus.Failed);

            // Act
            var filter = ProtobufUtils.ToPurgeInstanceFilter(request);

            // Assert
            Assert.NotNull(filter.RuntimeStatus);
            Assert.Contains(CoreOrchestrationStatus.Completed, filter.RuntimeStatus);
            Assert.Contains(CoreOrchestrationStatus.Failed, filter.RuntimeStatus);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ToPurgeInstanceFilter_EmptyStatusList_MapsToNull()
        {
            // Arrange
            var request = new P.PurgeInstancesRequest
            {
                PurgeInstanceFilter = new P.PurgeInstanceFilter
                {
                    CreatedTimeFrom = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                },
            };

            // Act
            var filter = ProtobufUtils.ToPurgeInstanceFilter(request);

            // Assert
            Assert.Null(filter.RuntimeStatus);
        }

        private static void AssertBufferReturned(TrackingByteArrayPool pool, string encoded)
        {
            int base64CharacterCount = encoded.Count(
                character => character != ' ' && character != '\t' && character != '\r' && character != '\n');
            Assert.Equal(1, pool.RentCount);
            Assert.Equal(checked((base64CharacterCount / 4) * 3), pool.MinimumLength);
            Assert.Equal(1, pool.ReturnCount);
            Assert.False(pool.ClearArray);
        }

        private sealed class TrackingByteArrayPool : ArrayPool<byte>
        {
            private byte[] rentedBuffer;

            public int RentCount { get; private set; }

            public int MinimumLength { get; private set; }

            public int ReturnCount { get; private set; }

            public bool ClearArray { get; private set; }

            public override byte[] Rent(int minimumLength)
            {
                Assert.Null(this.rentedBuffer);
                this.RentCount++;
                this.MinimumLength = minimumLength;

                // Dirty trailing bytes ensure the parser only reads the valid decoded segment.
                this.rentedBuffer = new byte[checked(minimumLength + 64)];
                Array.Fill(this.rentedBuffer, byte.MaxValue);
                return this.rentedBuffer;
            }

            public override void Return(byte[] array, bool clearArray = false)
            {
                Assert.Same(this.rentedBuffer, array);
                this.ReturnCount++;
                this.ClearArray = clearArray;

                // Overwrite returned storage so parsed messages cannot rely on pooled memory.
                Array.Fill(array, (byte)0);
            }
        }
    }
}
