// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#if FUNCTIONS_V3_OR_GREATER
#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DurableTask.Core;
using DurableTask.Core.Command;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class ProtobufUtils
    {
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
                        OrchestrationStatus = P.OrchestrationStatus.Completed,
                        Result = completedEvent.Result,
                    };
                    break;
                case EventType.ExecutionFailed:
                    var failedEvent = (ExecutionCompletedEvent)e;
                    payload.ExecutionCompleted = new P.ExecutionCompletedEvent
                    {
                        OrchestrationStatus = P.OrchestrationStatus.Failed,
                        Result = failedEvent.Result,
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
                        CorrelationData = startedEvent.Correlation,
                    };
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
                    };
                case P.OrchestratorAction.OrchestratorActionTypeOneofCase.CreateSubOrchestration:
                    return new CreateSubOrchestrationAction
                    {
                        Id = a.Id,
                        Input = a.CreateSubOrchestration.Input,
                        Name = a.CreateSubOrchestration.Name,
                        InstanceId = a.CreateSubOrchestration.InstanceId,
                        Tags = null, // TODO
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

                    return action;
                default:
                    throw new NotSupportedException($"Received unsupported action type '{a.OrchestratorActionTypeCase}'.");
            }
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
                failureDetails.IsNonRetriable);
        }

        internal static P.TaskFailureDetails? GetFailureDetails(FailureDetails? failureDetails)
        {
            if (failureDetails == null)
            {
                return null;
            }

            return new P.TaskFailureDetails
            {
                ErrorType = failureDetails.ErrorType,
                ErrorMessage = failureDetails.ErrorMessage,
                StackTrace = failureDetails.StackTrace,
                InnerFailure = GetFailureDetails(failureDetails.InnerFailure),
                IsNonRetriable = failureDetails.IsNonRetriable,
            };
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
            };

            // Empty lists are not allowed by the underlying code that takes in a OrchestrationQuery. However, protobuf
            // uses empty lists by default instead of nulls. Need to overwrite empty lists will null values.
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

            return new PurgeInstanceFilter(
                request.PurgeInstanceFilter.CreatedTimeFrom.ToDateTime(),
                request.PurgeInstanceFilter.CreatedTimeTo?.ToDateTime(),
                statusFilter);
        }

        internal static P.PurgeInstancesResponse CreatePurgeInstancesResponse(PurgeResult result)
        {
            return new P.PurgeInstancesResponse
            {
                DeletedInstanceCount = result.DeletedInstanceCount,
            };
        }
    }
}
#endif