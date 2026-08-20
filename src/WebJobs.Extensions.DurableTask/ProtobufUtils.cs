// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using DurableTask.Core;
using DurableTask.Core.Command;
using DurableTask.Core.Entities;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using DurableTask.Core.Tracing;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class ProtobufUtils
    {
        internal const string RollbackEntityOperationsOnExceptionsPropertyName = "RollbackEntityOperationsOnExceptions";

        public static P.HistoryEvent ToHistoryEventProto(HistoryEvent e)
        {
            var payload = new P.HistoryEvent()
            {
                EventId = e.EventId,
                Timestamp = Timestamp.FromDateTime(e.Timestamp),
            };

            switch (e.EventType)
            {
                case EventType.ContinueAsNew:
                    var continueAsNew = (ContinueAsNewEvent)e;
                    payload.ContinueAsNew = new P.ContinueAsNewEvent
                    {
                        Input = continueAsNew.Result,
                    };
                    break;
                case EventType.EventRaised:
                    var eventRaised = (EventRaisedEvent)e;
                    payload.EventRaised = new P.EventRaisedEvent
                    {
                        Name = eventRaised.Name,
                        Input = eventRaised.Input,
                    };
                    break;
                case EventType.EventSent:
                    var eventSent = (EventSentEvent)e;
                    payload.EventSent = new P.EventSentEvent
                    {
                        Name = eventSent.Name,
                        Input = eventSent.Input,
                        InstanceId = eventSent.InstanceId,
                    };
                    break;
                case EventType.ExecutionCompleted:
                    var completedEvent = (ExecutionCompletedEvent)e;
                    payload.ExecutionCompleted = new P.ExecutionCompletedEvent
                    {
                        OrchestrationStatus = (P.OrchestrationStatus)completedEvent.OrchestrationStatus,
                        Result = completedEvent.Result,
                        FailureDetails = GetFailureDetails(completedEvent.FailureDetails),
                    };
                    break;
                case EventType.ExecutionStarted:
                    // Start of a new orchestration instance
                    var startedEvent = (ExecutionStartedEvent)e;
                    payload.ExecutionStarted = new P.ExecutionStartedEvent
                    {
                        Name = startedEvent.Name,
                        Version = startedEvent.Version,
                        Input = startedEvent.Input,
                        OrchestrationInstance = new P.OrchestrationInstance
                        {
                            InstanceId = startedEvent.OrchestrationInstance.InstanceId,
                            ExecutionId = startedEvent.OrchestrationInstance.ExecutionId,
                        },
                        ParentInstance = startedEvent.ParentInstance == null ? null : new P.ParentInstanceInfo
                        {
                            Name = startedEvent.ParentInstance.Name,
                            Version = startedEvent.ParentInstance.Version,
                            TaskScheduledId = startedEvent.ParentInstance.TaskScheduleId,
                            OrchestrationInstance = new P.OrchestrationInstance
                            {
                                InstanceId = startedEvent.ParentInstance.OrchestrationInstance.InstanceId,
                                ExecutionId = startedEvent.ParentInstance.OrchestrationInstance.ExecutionId,
                            },
                        },
                        ScheduledStartTimestamp = startedEvent.ScheduledStartTime == null ? null : Timestamp.FromDateTime(startedEvent.ScheduledStartTime.Value),
                        ParentTraceContext = startedEvent.ParentTraceContext == null ? null : new P.TraceContext
                        {
                            TraceParent = startedEvent.ParentTraceContext.TraceParent,
                            TraceState = startedEvent.ParentTraceContext.TraceState,
                        },
                    };

                    if (startedEvent.Tags != null)
                    {
                        foreach (KeyValuePair<string, string> tag in startedEvent.Tags)
                        {
                            payload.ExecutionStarted.Tags[tag.Key] = tag.Value;
                        }
                    }

                    break;
                case EventType.ExecutionTerminated:
                    var terminatedEvent = (ExecutionTerminatedEvent)e;
                    payload.ExecutionTerminated = new P.ExecutionTerminatedEvent
                    {
                        Input = terminatedEvent.Input,
                    };
                    break;
                case EventType.TaskScheduled:
                    var taskScheduledEvent = (TaskScheduledEvent)e;
                    payload.TaskScheduled = new P.TaskScheduledEvent
                    {
                        Name = taskScheduledEvent.Name,
                        Version = taskScheduledEvent.Version,
                        Input = taskScheduledEvent.Input,
                    };

                    if (taskScheduledEvent.Tags != null)
                    {
                        foreach (KeyValuePair<string, string> tag in taskScheduledEvent.Tags)
                        {
                            payload.TaskScheduled.Tags[tag.Key] = tag.Value;
                        }
                    }

                    break;
                case EventType.TaskCompleted:
                    var taskCompletedEvent = (TaskCompletedEvent)e;
                    payload.TaskCompleted = new P.TaskCompletedEvent
                    {
                        Result = taskCompletedEvent.Result,
                        TaskScheduledId = taskCompletedEvent.TaskScheduledId,
                    };
                    break;
                case EventType.TaskFailed:
                    var taskFailedEvent = (TaskFailedEvent)e;
                    payload.TaskFailed = new P.TaskFailedEvent
                    {
                        FailureDetails = GetFailureDetails(taskFailedEvent.FailureDetails),
                        TaskScheduledId = taskFailedEvent.TaskScheduledId,
                    };
                    break;
                case EventType.SubOrchestrationInstanceCreated:
                    var subOrchestrationCreated = (SubOrchestrationInstanceCreatedEvent)e;
                    payload.SubOrchestrationInstanceCreated = new P.SubOrchestrationInstanceCreatedEvent
                    {
                        Input = subOrchestrationCreated.Input,
                        InstanceId = subOrchestrationCreated.InstanceId,
                        Name = subOrchestrationCreated.Name,
                        Version = subOrchestrationCreated.Version,
                    };
                    break;
                case EventType.SubOrchestrationInstanceCompleted:
                    var subOrchestrationCompleted = (SubOrchestrationInstanceCompletedEvent)e;
                    payload.SubOrchestrationInstanceCompleted = new P.SubOrchestrationInstanceCompletedEvent
                    {
                        Result = subOrchestrationCompleted.Result,
                        TaskScheduledId = subOrchestrationCompleted.TaskScheduledId,
                    };
                    break;
                case EventType.SubOrchestrationInstanceFailed:
                    var subOrchestrationFailed = (SubOrchestrationInstanceFailedEvent)e;
                    payload.SubOrchestrationInstanceFailed = new P.SubOrchestrationInstanceFailedEvent
                    {
                        FailureDetails = GetFailureDetails(subOrchestrationFailed.FailureDetails),
                        TaskScheduledId = subOrchestrationFailed.TaskScheduledId,
                    };
                    break;
                case EventType.TimerCreated:
                    var timerCreatedEvent = (TimerCreatedEvent)e;
                    payload.TimerCreated = new P.TimerCreatedEvent
                    {
                        FireAt = Timestamp.FromDateTime(timerCreatedEvent.FireAt),
                    };
                    break;
                case EventType.TimerFired:
                    var timerFiredEvent = (TimerFiredEvent)e;
                    payload.TimerFired = new P.TimerFiredEvent
                    {
                        FireAt = Timestamp.FromDateTime(timerFiredEvent.FireAt),
                        TimerId = timerFiredEvent.TimerId,
                    };
                    break;
                case EventType.OrchestratorStarted:
                    // This event has no data
                    payload.OrchestratorStarted = new P.OrchestratorStartedEvent();
                    break;
                case EventType.OrchestratorCompleted:
                    // This event has no data
                    payload.OrchestratorCompleted = new P.OrchestratorCompletedEvent();
                    break;
                case EventType.GenericEvent:
                    var genericEvent = (GenericEvent)e;
                    payload.GenericEvent = new P.GenericEvent
                    {
                        Data = genericEvent.Data,
                    };
                    break;
                case EventType.HistoryState:
                    var historyStateEvent = (HistoryStateEvent)e;
                    payload.HistoryState = new P.HistoryStateEvent
                    {
                        OrchestrationState = new P.OrchestrationState
                        {
                            InstanceId = historyStateEvent.State.OrchestrationInstance.InstanceId,
                            Name = historyStateEvent.State.Name,
                            Version = historyStateEvent.State.Version,
                            ParentInstanceId = historyStateEvent.State.ParentInstance?.OrchestrationInstance?.InstanceId,
                            Input = historyStateEvent.State.Input,
                            Output = historyStateEvent.State.Output,
                            ScheduledStartTimestamp = historyStateEvent.State.ScheduledStartTime == null ? null : Timestamp.FromDateTime(historyStateEvent.State.ScheduledStartTime.Value),
                            CreatedTimestamp = Timestamp.FromDateTime(historyStateEvent.State.CreatedTime),
                            LastUpdatedTimestamp = Timestamp.FromDateTime(historyStateEvent.State.LastUpdatedTime),
                            OrchestrationStatus = (P.OrchestrationStatus)historyStateEvent.State.OrchestrationStatus,
                            CustomStatus = historyStateEvent.State.Status,
                        },
                    };
                    break;
                case EventType.ExecutionSuspended:
                    var suspendedEvent = (ExecutionSuspendedEvent)e;
                    payload.ExecutionSuspended = new P.ExecutionSuspendedEvent
                    {
                        Input = suspendedEvent.Reason,
                    };
                    break;
                case EventType.ExecutionResumed:
                    var resumedEvent = (ExecutionResumedEvent)e;
                    payload.ExecutionResumed = new P.ExecutionResumedEvent
                    {
                        Input = resumedEvent.Reason,
                    };
                    break;
                case EventType.ExecutionRewound:
                    payload.ExecutionRewound = new P.ExecutionRewoundEvent();
                    break;
                default:
                    throw new NotSupportedException($"Found unsupported history event '{e.EventType}'.");
            }

            return payload;
        }

        public static OrchestratorAction ToOrchestratorAction(P.OrchestratorAction a)
        {
            switch (a.OrchestratorActionTypeCase)
            {
                case P.OrchestratorAction.OrchestratorActionTypeOneofCase.ScheduleTask:
                    return new ScheduleTaskOrchestratorAction
                    {
                        Id = a.Id,
                        Input = a.ScheduleTask.Input,
                        Name = a.ScheduleTask.Name,
                        Version = a.ScheduleTask.Version,
                        Tags = a.ScheduleTask.Tags.ToDictionary(),
                    };
                case P.OrchestratorAction.OrchestratorActionTypeOneofCase.CreateSubOrchestration:
                    return new CreateSubOrchestrationAction
                    {
                        Id = a.Id,
                        Input = a.CreateSubOrchestration.Input,
                        Name = a.CreateSubOrchestration.Name,
                        InstanceId = a.CreateSubOrchestration.InstanceId,
                        Tags = a.CreateSubOrchestration.Tags.ToDictionary(),
                        Version = a.CreateSubOrchestration.Version,
                    };
                case P.OrchestratorAction.OrchestratorActionTypeOneofCase.CreateTimer:
                    return new CreateTimerOrchestratorAction
                    {
                        Id = a.Id,
                        FireAt = a.CreateTimer.FireAt.ToDateTime(),
                    };
                case P.OrchestratorAction.OrchestratorActionTypeOneofCase.SendEvent:
                    return new SendEventOrchestratorAction
                    {
                        Id = a.Id,
                        Instance = new OrchestrationInstance
                        {
                            InstanceId = a.SendEvent.Instance.InstanceId,
                            ExecutionId = a.SendEvent.Instance.ExecutionId,
                        },
                        EventName = a.SendEvent.Name,
                        EventData = a.SendEvent.Data,
                    };
                case P.OrchestratorAction.OrchestratorActionTypeOneofCase.CompleteOrchestration:
                    P.CompleteOrchestrationAction? completedAction = a.CompleteOrchestration;
                    var action = new OrchestrationCompleteOrchestratorAction
                    {
                        Id = a.Id,
                        OrchestrationStatus = (OrchestrationStatus)completedAction.OrchestrationStatus,
                        Result = completedAction.Result,
                        Details = completedAction.Details,
                        FailureDetails = GetFailureDetails(completedAction.FailureDetails),
                        NewVersion = completedAction.NewVersion,
                    };

                    if (completedAction.CarryoverEvents?.Count > 0)
                    {
                        foreach (P.HistoryEvent e in completedAction.CarryoverEvents)
                        {
                            // Only raised events are supported for carryover
                            if (e.EventRaised is P.EventRaisedEvent eventRaised)
                            {
                                action.CarryoverEvents.Add(new EventRaisedEvent(e.EventId, eventRaised.Input)
                                {
                                    Name = eventRaised.Name,
                                });
                            }
                        }
                    }

                    foreach (KeyValuePair<string, string> kvp in completedAction.Tags)
                    {
                        action.Tags[kvp.Key] = kvp.Value;
                    }

                    return action;
                case P.OrchestratorAction.OrchestratorActionTypeOneofCase.SendEntityMessage:
                    RequestMessage? entityMessage = null;
                    string? eventName = null;
                    string? targetInstance = null;
                    DateTime? scheduledTime = null;
                    switch (a.SendEntityMessage.EntityMessageTypeCase)
                    {
                        case P.SendEntityMessageAction.EntityMessageTypeOneofCase.EntityLockRequested:
                            entityMessage = new RequestMessage()
                            {
                                Operation = null,
                                Id = Guid.Parse(a.SendEntityMessage.EntityLockRequested.CriticalSectionId),
                                LockSet = a.SendEntityMessage.EntityLockRequested.LockSet.Select(s => EntityId.FromString(s)).ToArray(),
                                Position = a.SendEntityMessage.EntityLockRequested.Position,
                                ParentInstanceId = a.SendEntityMessage.EntityLockRequested.ParentInstanceId,
                            };
                            targetInstance = a.SendEntityMessage.EntityLockRequested.LockSet.ElementAt(a.SendEntityMessage.EntityLockRequested.Position);
                            eventName = EntityMessageEventNames.RequestMessageEventName;
                            break;
                        case P.SendEntityMessageAction.EntityMessageTypeOneofCase.EntityUnlockSent:
                            entityMessage = new RequestMessage()
                            {
                                Id = Guid.Parse(a.SendEntityMessage.EntityUnlockSent.CriticalSectionId),
                                ParentInstanceId = a.SendEntityMessage.EntityUnlockSent.ParentInstanceId,
                            };
                            targetInstance = a.SendEntityMessage.EntityUnlockSent.TargetInstanceId;
                            eventName = EntityMessageEventNames.ReleaseMessageEventName;
                            break;
                        case P.SendEntityMessageAction.EntityMessageTypeOneofCase.EntityOperationCalled:
                            entityMessage = new RequestMessage()
                            {
                                Operation = a.SendEntityMessage.EntityOperationCalled.Operation,
                                IsSignal = false,
                                Input = a.SendEntityMessage.EntityOperationCalled.Input,
                                Id = Guid.Parse(a.SendEntityMessage.EntityOperationCalled.RequestId),
                                ScheduledTime = a.SendEntityMessage.EntityOperationCalled.ScheduledTime?.ToDateTime(),
                                ParentInstanceId = a.SendEntityMessage.EntityOperationCalled.ParentInstanceId,
                                ParentExecutionId = a.SendEntityMessage.EntityOperationCalled.ParentExecutionId,
                            };
                            targetInstance = a.SendEntityMessage.EntityOperationCalled.TargetInstanceId;
                            scheduledTime = a.SendEntityMessage.EntityOperationCalled.ScheduledTime?.ToDateTime();
                            eventName = scheduledTime.HasValue
                                ? EntityMessageEventNames.ScheduledRequestMessageEventName(scheduledTime.Value)
                                : EntityMessageEventNames.RequestMessageEventName;

                            break;
                        case P.SendEntityMessageAction.EntityMessageTypeOneofCase.EntityOperationSignaled:
                            entityMessage = new RequestMessage()
                            {
                                Operation = a.SendEntityMessage.EntityOperationSignaled.Operation,
                                IsSignal = true,
                                Input = a.SendEntityMessage.EntityOperationSignaled.Input,
                                Id = Guid.Parse(a.SendEntityMessage.EntityOperationSignaled.RequestId),
                                ScheduledTime = a.SendEntityMessage.EntityOperationSignaled.ScheduledTime?.ToDateTime(),
                            };
                            targetInstance = a.SendEntityMessage.EntityOperationSignaled.TargetInstanceId;
                            scheduledTime = a.SendEntityMessage.EntityOperationSignaled.ScheduledTime?.ToDateTime();
                            eventName = scheduledTime.HasValue
                                ? EntityMessageEventNames.ScheduledRequestMessageEventName(scheduledTime.Value)
                                : EntityMessageEventNames.RequestMessageEventName;

                            break;
                        default:
                            throw new NotSupportedException($"Deserialization of SendEntityMessage action type '{a.SendEntityMessage.EntityMessageTypeCase}' is not supported.");
                    }

                    return new SendEventOrchestratorAction
                    {
                        Id = a.Id,
                        Instance = new OrchestrationInstance
                        {
                            InstanceId = targetInstance,
                        },
                        EventName = eventName,
                        EventData = JsonConvert.SerializeObject(entityMessage, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.None }),
                    };
                default:
                    throw new NotSupportedException($"Received unsupported action type '{a.OrchestratorActionTypeCase}'.");
            }
        }

        [return: NotNullIfNotNull("parameters")]
        public static P.OrchestratorEntityParameters? ToProtobuf(this TaskOrchestrationEntityParameters? parameters)
        {
            if (parameters == null)
            {
                return null;
            }

            return new P.OrchestratorEntityParameters
            {
                EntityMessageReorderWindow = Duration.FromTimeSpan(parameters.EntityMessageReorderWindow),
            };
        }

        public static string Base64Encode(IMessage message)
        {
            // Create a serialized payload using lower-level protobuf APIs. We do this to avoid allocating
            // byte[] arrays for every request, which would otherwise put a heavy burden on the GC. Unfortunately
            // the protobuf API version we're using doesn't currently have memory-efficient serialization APIs.
            int messageSize = message.CalculateSize();
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(messageSize);
            try
            {
                using var intermediateBufferStream = new MemoryStream(rentedBuffer, 0, messageSize);
                using var protobufOutputStream = new CodedOutputStream(intermediateBufferStream);
                protobufOutputStream.WriteRawMessage(message);
                protobufOutputStream.Flush();
                return Convert.ToBase64String(rentedBuffer, 0, messageSize);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        internal static FailureDetails? GetFailureDetails(P.TaskFailureDetails? failureDetails)
        {
            if (failureDetails == null)
            {
                return null;
            }

            return new FailureDetails(
                failureDetails.ErrorType,
                failureDetails.ErrorMessage,
                failureDetails.StackTrace,
                GetFailureDetails(failureDetails.InnerFailure),
                failureDetails.IsNonRetriable,
                properties: ConvertProperties(failureDetails.Properties));
        }

        internal static P.TaskFailureDetails? GetFailureDetails(FailureDetails? failureDetails)
        {
            if (failureDetails == null)
            {
                return null;
            }

            var taskFailure = new P.TaskFailureDetails
            {
                ErrorType = failureDetails.ErrorType,
                ErrorMessage = failureDetails.ErrorMessage,
                StackTrace = failureDetails.StackTrace,
                InnerFailure = GetFailureDetails(failureDetails.InnerFailure),
                IsNonRetriable = failureDetails.IsNonRetriable,
            };

            if (failureDetails.Properties != null)
            {
                foreach (var kvp in failureDetails.Properties)
                {
                    taskFailure.Properties[kvp.Key] = ConvertObjectToValue(kvp.Value);
                }
            }

            return taskFailure;
        }

        internal static OrchestrationQuery ToOrchestrationQuery(P.QueryInstancesRequest request)
        {
            var query = new OrchestrationQuery()
            {
                RuntimeStatus = request.Query.RuntimeStatus?.Select(status => (OrchestrationStatus)status).ToList(),
                CreatedTimeFrom = request.Query.CreatedTimeFrom?.ToDateTime(),
                CreatedTimeTo = request.Query.CreatedTimeTo?.ToDateTime(),
                TaskHubNames = request.Query.TaskHubNames,
                PageSize = request.Query.MaxInstanceCount,
                ContinuationToken = request.Query.ContinuationToken,
                InstanceIdPrefix = request.Query.InstanceIdPrefix,
                FetchInputsAndOutputs = request.Query.FetchInputsAndOutputs,
                ExcludeEntities = true,
            };

            // Empty lists are not allowed by the underlying code that takes in an OrchestrationQuery. However,
            // some clients use empty lists instead of nulls. Need to overwrite empty lists with null values.
            if (query.TaskHubNames?.Count == 0)
            {
                query.TaskHubNames = null;
            }

            if (query.RuntimeStatus?.Count == 0)
            {
                query.RuntimeStatus = null;
            }

            return query;
        }

        internal static P.QueryInstancesResponse CreateQueryInstancesResponse(OrchestrationQueryResult result, P.QueryInstancesRequest request)
        {
            var response = new P.QueryInstancesResponse { ContinuationToken = result.ContinuationToken };

            foreach (OrchestrationState state in result.OrchestrationState)
            {
                var orchestrationState = new P.OrchestrationState
                {
                    InstanceId = state.OrchestrationInstance.InstanceId,
                    Name = state.Name,
                    Version = state.Version,
                    ParentInstanceId = state.ParentInstance?.OrchestrationInstance?.InstanceId,
                    Input = state.Input,
                    Output = state.Output,
                    ScheduledStartTimestamp = state.ScheduledStartTime == null ? null : Timestamp.FromDateTime(state.ScheduledStartTime.Value),
                    CreatedTimestamp = Timestamp.FromDateTime(state.CreatedTime),
                    LastUpdatedTimestamp = Timestamp.FromDateTime(state.LastUpdatedTime),
                    OrchestrationStatus = (P.OrchestrationStatus)state.OrchestrationStatus,
                    CustomStatus = state.Status,
                };

                response.OrchestrationState.Add(orchestrationState);
            }

            return response;
        }

        internal static PurgeInstanceFilter ToPurgeInstanceFilter(P.PurgeInstancesRequest request)
        {
            // Empty lists are not allowed by the underlying code that takes in a PurgeInstanceFilter. However, some
            // clients (like Java) may use empty lists by default instead of nulls.
            // Long story short: we must make sure to only copy over the list if it's non-empty.
            IEnumerable<OrchestrationStatus>? statusFilter = null;
            if (request.PurgeInstanceFilter.RuntimeStatus != null && request.PurgeInstanceFilter.RuntimeStatus.Count > 0)
            {
                statusFilter = request.PurgeInstanceFilter.RuntimeStatus?.Select(status => (OrchestrationStatus)status).ToList();
            }

            // This ternary condition is necessary because the protobuf spec __insists__ that CreatedTimeFrom may never be null,
            // but nonetheless if you pass null in function code, the value will be null here
            var filter = new PurgeInstanceFilter(
                request.PurgeInstanceFilter.CreatedTimeFrom == null ? DateTime.MinValue : request.PurgeInstanceFilter.CreatedTimeFrom.ToDateTime(),
                request.PurgeInstanceFilter.CreatedTimeTo?.ToDateTime(),
                statusFilter);

            if (request.PurgeInstanceFilter.Timeout != null)
            {
                filter.Timeout = ToNonNegativeTimeSpan(request.PurgeInstanceFilter.Timeout);
            }

            return filter;
        }

        private static TimeSpan ToNonNegativeTimeSpan(Duration timeout)
        {
            const long MaxDurationSeconds = 315576000000L;
            const int MaxDurationNanos = 999999999;

            if (timeout.Seconds < 0 || timeout.Nanos < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be non-negative.");
            }

            if (timeout.Seconds > MaxDurationSeconds || timeout.Nanos > MaxDurationNanos)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout is outside the valid protobuf Duration range.");
            }

            return timeout.ToTimeSpan();
        }

        internal static P.PurgeInstancesResponse CreatePurgeInstancesResponse(PurgeResult result)
        {
            var response = new P.PurgeInstancesResponse
            {
                DeletedInstanceCount = result.DeletedInstanceCount,
            };

            if (result.IsComplete.HasValue)
            {
                response.IsComplete = result.IsComplete.Value;
            }

            return response;
        }

        /// <summary>
        /// Converts a <see cref="EntityBatchRequest" /> to <see cref="P.EntityBatchRequest" />.
        /// </summary>
        /// <param name="entityBatchRequest">The operation request to convert.</param>
        /// <param name="configurations">The remote instance configuration options for this batch request.</param>
        /// <param name="rollbackEntityOperationsOnExceptions">Whether failed entity operations should be rolled back.</param>
        /// <returns>The converted operation request.</returns>
        [return: NotNullIfNotNull("entityBatchRequest")]
        internal static P.EntityBatchRequest? ToEntityBatchRequest(
            this EntityBatchRequest? entityBatchRequest,
            RemoteInstanceConfiguration? configurations,
            bool? rollbackEntityOperationsOnExceptions = null)
        {
            if (entityBatchRequest == null)
            {
                return null;
            }

            var batchRequest = new P.EntityBatchRequest()
            {
                InstanceId = entityBatchRequest.InstanceId,
                EntityState = configurations?.IncludeState == false ? null : entityBatchRequest.EntityState,
            };

            batchRequest.Properties.Add(ProtobufUtils.ConvertPocoToProtoMap(configurations));
            if (rollbackEntityOperationsOnExceptions.HasValue)
            {
                batchRequest.Properties[RollbackEntityOperationsOnExceptionsPropertyName] =
                    Value.ForBool(rollbackEntityOperationsOnExceptions.Value);
            }

            foreach (var operation in entityBatchRequest.Operations ?? Enumerable.Empty<OperationRequest>())
            {
                batchRequest.Operations.Add(operation.ToOperationRequest());
            }

            return batchRequest;
        }

        /// <summary>
        /// Converts a <see cref="OperationRequest" /> to <see cref="P.OperationRequest" />.
        /// </summary>
        /// <param name="operationRequest">The operation request to convert.</param>
        /// <returns>The converted operation request.</returns>
        [return: NotNullIfNotNull("operationRequest")]
        internal static P.OperationRequest? ToOperationRequest(this OperationRequest? operationRequest)
        {
            if (operationRequest == null)
            {
                return null;
            }

            return new P.OperationRequest()
            {
                Operation = operationRequest.Operation,
                Input = operationRequest.Input,
                RequestId = operationRequest.Id.ToString(),
                TraceContext = operationRequest.TraceContext != null ? new P.TraceContext
                {
                    TraceParent = operationRequest.TraceContext.TraceParent,
                    TraceState = operationRequest.TraceContext.TraceState,
                }
                : null,
            };
        }

        /// <summary>
        /// Converts a <see cref="P.EntityBatchResult" /> to a <see cref="OperationBatchResult" />.
        /// </summary>
        /// <param name="entityBatchResult">The operation result to convert.</param>
        /// <param name="defaultVersion">The default version to use for orchestrations started by the entity when no explicit version is specified.</param>
        /// <returns>The converted operation result.</returns>
        [return: NotNullIfNotNull("entityBatchResult")]
        internal static EntityBatchResult? ToEntityBatchResult(this P.EntityBatchResult? entityBatchResult, string? defaultVersion = null)
        {
            if (entityBatchResult == null)
            {
                return null;
            }

            return new EntityBatchResult()
            {
                Actions = entityBatchResult.Actions.Select(operationAction => operationAction!.ToOperationAction(defaultVersion)).ToList(),
                EntityState = entityBatchResult.EntityState,
                Results = entityBatchResult.Results.Select(operationResult => operationResult!.ToOperationResult()).ToList(),
                FailureDetails = GetFailureDetails(entityBatchResult.FailureDetails),
            };
        }

        /// <summary>
        /// Converts a <see cref="P.OperationAction" /> to a <see cref="OperationAction" />.
        /// </summary>
        /// <param name="operationAction">The operation action to convert.</param>
        /// <param name="defaultVersion">The default version to use for orchestrations started by the entity when no explicit version is specified.</param>
        /// <returns>The converted operation action.</returns>
        [return: NotNullIfNotNull("operationAction")]
        internal static OperationAction? ToOperationAction(this P.OperationAction? operationAction, string? defaultVersion = null)
        {
            if (operationAction == null)
            {
                return null;
            }

            switch (operationAction.OperationActionTypeCase)
            {
                case P.OperationAction.OperationActionTypeOneofCase.SendSignal:

                    return new SendSignalOperationAction()
                    {
                        Name = operationAction.SendSignal.Name,
                        Input = operationAction.SendSignal.Input,
                        InstanceId = operationAction.SendSignal.InstanceId,
                        ScheduledTime = operationAction.SendSignal.ScheduledTime?.ToDateTime(),
                        RequestTime = operationAction.SendSignal.RequestTime?.ToDateTimeOffset(),
                        ParentTraceContext = operationAction.SendSignal.ParentTraceContext != null ?
                            new DistributedTraceContext(
                                operationAction.SendSignal.ParentTraceContext.TraceParent,
                                operationAction.SendSignal.ParentTraceContext.TraceState)
                            : null,
                    };

                case P.OperationAction.OperationActionTypeOneofCase.StartNewOrchestration:
                    // Apply the default version if no explicit version is provided.
                    // This ensures orchestrations scheduled from entities use the host's default version,
                    // consistent with orchestrations started via the client or from other orchestrations.
                    string? version = operationAction.StartNewOrchestration.Version;
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        version = defaultVersion;
                    }

                    return new StartNewOrchestrationOperationAction()
                    {
                        Name = operationAction.StartNewOrchestration.Name,
                        Input = operationAction.StartNewOrchestration.Input,
                        InstanceId = operationAction.StartNewOrchestration.InstanceId,
                        Version = version,
                        RequestTime = operationAction.StartNewOrchestration.RequestTime?.ToDateTimeOffset(),
                        ScheduledStartTime = operationAction.StartNewOrchestration.ScheduledTime?.ToDateTime(),
                        ParentTraceContext = operationAction.StartNewOrchestration.ParentTraceContext != null ?
                            new DistributedTraceContext(
                                operationAction.StartNewOrchestration.ParentTraceContext.TraceParent,
                                operationAction.StartNewOrchestration.ParentTraceContext.TraceState)
                            : null,
                    };
                default:
                    throw new NotSupportedException($"Deserialization of {operationAction.OperationActionTypeCase} is not supported.");
            }
        }

        /// <summary>
        /// Converts a <see cref="P.OperationResult" /> to a <see cref="OperationResult" />.
        /// </summary>
        /// <param name="operationResult">The operation result to convert.</param>
        /// <returns>The converted operation result.</returns>
        [return: NotNullIfNotNull("operationResult")]
        internal static OperationResult? ToOperationResult(this P.OperationResult? operationResult)
        {
            if (operationResult == null)
            {
                return null;
            }

            switch (operationResult.ResultTypeCase)
            {
                case P.OperationResult.ResultTypeOneofCase.Success:
                    return new OperationResult()
                    {
                        Result = operationResult.Success.Result,
                        StartTimeUtc = operationResult.Success.StartTimeUtc?.ToDateTime(),
                        EndTimeUtc = operationResult.Success.EndTimeUtc?.ToDateTime(),
                    };

                case P.OperationResult.ResultTypeOneofCase.Failure:
                    return new OperationResult()
                    {
                        FailureDetails = GetFailureDetails(operationResult.Failure.FailureDetails),
                        StartTimeUtc = operationResult.Failure.StartTimeUtc?.ToDateTime(),
                        EndTimeUtc = operationResult.Failure.EndTimeUtc?.ToDateTime(),
                    };

                default:
                    throw new NotSupportedException($"Deserialization of {operationResult.ResultTypeCase} is not supported.");
            }
        }

        internal static IDictionary<string, object?> ConvertProperties(MapField<string, Value> properties)
        {
            return properties.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertValueToObject(kvp.Value));
        }

        internal static MapField<string, Value> ConvertPocoToProtoMap(object? configurations)
        {
            var map = new MapField<string, Value>();

            if (configurations == null)
            {
                return map;
            }

            System.Type type = configurations.GetType();
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (PropertyInfo property in properties)
            {
                string propertyName = property.Name;
                object? propertyValue = property.GetValue(configurations);

                map[propertyName] = ConvertObjectToValue(propertyValue);
            }

            return map;
        }

        internal static Value ConvertObjectToValue(object? obj)
        {
            return obj switch
            {
                null => Value.ForNull(),
                string str => Value.ForString(str),
                bool b => Value.ForBool(b),
                int i => Value.ForNumber(i),
                long l => Value.ForNumber(l),
                float f => Value.ForNumber(f),
                double d => Value.ForNumber(d),
                decimal dec => Value.ForNumber((double)dec),

                // Handle Newtonsoft.Json types (JValue, JArray, JObject) from deserialized protobuf
                JValue jv => ConvertJValueToValue(jv),
                JArray ja => Value.ForList(ja.Select(ConvertObjectToValue).ToArray()),
                JObject jo => Value.ForStruct(new Struct
                {
                    Fields = { jo.Properties().ToDictionary(p => p.Name, p => ConvertObjectToValue(p.Value)) },
                }),

                // For DateTime and DateTimeOffset, serialize to string directly.
                DateTime dt => Value.ForString(dt.ToString("O")),
                DateTimeOffset dto => Value.ForString(dto.ToString("O")),
                IDictionary<string, object?> dict => Value.ForStruct(new Struct
                {
                    Fields = { dict.ToDictionary(kvp => kvp.Key, kvp => ConvertObjectToValue(kvp.Value)) },
                }),
                IEnumerable e => Value.ForList(e.Cast<object?>().Select(ConvertObjectToValue).ToArray()),

                // Fallback: convert unlisted type to string.
                _ => Value.ForString(obj.ToString() ?? string.Empty),
            };
        }

        internal static Value ConvertJValueToValue(JValue jv)
        {
            return jv.Type switch
            {
                JTokenType.Null => Value.ForNull(),
                JTokenType.String => Value.ForString(jv.Value<string>() ?? string.Empty),
                JTokenType.Boolean => Value.ForBool(jv.Value<bool>()),
                JTokenType.Integer => Value.ForNumber(jv.Value<long>()),
                JTokenType.Float => Value.ForNumber(jv.Value<double>()),
                JTokenType.Date => Value.ForString($"dt:{jv.Value<DateTime>().ToString("O")}"),
                _ => Value.ForString(jv.ToString()),
            };
        }

        internal static object? ConvertValueToObject(Google.Protobuf.WellKnownTypes.Value value)
        {
            switch (value.KindCase)
            {
                case Google.Protobuf.WellKnownTypes.Value.KindOneofCase.NullValue:
                    return null;
                case Google.Protobuf.WellKnownTypes.Value.KindOneofCase.NumberValue:
                    return value.NumberValue;
                case Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StringValue:
                    return value.StringValue;
                case Google.Protobuf.WellKnownTypes.Value.KindOneofCase.BoolValue:
                    return value.BoolValue;
                case Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StructValue:
                    return value.StructValue.Fields.ToDictionary(
                        pair => pair.Key,
                        pair => ConvertValueToObject(pair.Value));
                case Google.Protobuf.WellKnownTypes.Value.KindOneofCase.ListValue:
                    return value.ListValue.Values.Select(ConvertValueToObject).ToList();
                default:
                    // Fallback: serialize the whole value to JSON string
                    return JsonConvert.SerializeObject(value);
            }
        }
    }
}
