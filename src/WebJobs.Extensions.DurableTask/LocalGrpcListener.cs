// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Entities;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using DurableTask.Core.Serializing.Internal;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DTCore = DurableTask.Core;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LocalGrpcListener : IHostedService
    {
        private const int DefaultPort = 4001;

        // Pick a large, fixed range of ports that are going to be valid in all environment.
        // Avoiding ports below 1024 as those are blocked by app service sandbox.
        // Ephemeral ports for most OS start well above 32768. See https://www.ncftp.com/ncftpd/doc/misc/ephemeral_ports.html
        private const int MinPort = 30000;
        private const int MaxPort = 31000;

        private readonly DurableTaskExtension extension;

        private readonly Random portGenerator;
        private readonly HashSet<int> attemptedPorts;

        private Server? grpcServer;

        public LocalGrpcListener(DurableTaskExtension extension)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));

            this.portGenerator = new Random();
            this.attemptedPorts = new HashSet<int>();
        }

        public string? ListenAddress { get; private set; }

        public async Task StartAsync(CancellationToken cancelToken = default)
        {
            const int maxAttempts = 10;
            int numAttempts = 1;
            while (numAttempts <= maxAttempts)
            {
                ChannelOption[] options = new[]
                {
                    new ChannelOption(ChannelOptions.MaxReceiveMessageLength, int.MaxValue),
                    new ChannelOption(ChannelOptions.MaxSendMessageLength, int.MaxValue),
                };

                if (this.grpcServer != null)
                {
                    await this.grpcServer.ShutdownAsync();
                }

                this.grpcServer = new Server(options);
                this.grpcServer.Services.Add(P.TaskHubSidecarService.BindService(new TaskHubGrpcServer(this)));

                int listeningPort = numAttempts == 1 ? DefaultPort : this.GetRandomPort();
                int portBindingResult = this.grpcServer.Ports.Add("localhost", listeningPort, ServerCredentials.Insecure);
                if (portBindingResult != 0)
                {
                    try
                    {
                        this.grpcServer.Start();
                        this.ListenAddress = $"http://localhost:{listeningPort}";

                        this.extension.TraceHelper.ExtensionInformationalEvent(
                            this.extension.Options.HubName,
                            instanceId: string.Empty,
                            functionName: string.Empty,
                            message: $"Opened local gRPC endpoint: {this.ListenAddress}",
                            writeToUserLogs: true);

                        return;
                    }
                    catch (IOException)
                    {
                        portBindingResult = 0;
                    }
                }

                if (portBindingResult == 0)
                {
                    this.extension.TraceHelper.ExtensionWarningEvent(
                        this.extension.Options.HubName,
                        functionName: string.Empty,
                        instanceId: string.Empty,
                        message: $"Failed to open local port {listeningPort}. This was attempt #{numAttempts} to open a local port.");
                    this.attemptedPorts.Add(listeningPort);
                    numAttempts++;
                }
            }

            throw new IOException($"Unable to find a port to open an RPC endpoint on after {maxAttempts} attempts");
        }

        public async Task StopAsync(CancellationToken cancelToken = default)
        {
            if (this.grpcServer != null)
            {
                await this.grpcServer.ShutdownAsync();
            }
        }

        private int GetRandomPort()
        {
            // Get a random port that has not already been attempted so we don't waste time trying
            // to listen to a port we know is not free.
            int randomPort;
            do
            {
                randomPort = this.portGenerator.Next(MinPort, MaxPort);
            }
            while (this.attemptedPorts.Contains(randomPort));

            return randomPort;
        }

        private class TaskHubGrpcServer : P.TaskHubSidecarService.TaskHubSidecarServiceBase
        {
            private readonly DurableTaskExtension extension;

            public TaskHubGrpcServer(LocalGrpcListener listener)
            {
                this.extension = listener.extension;
            }

            public override Task<Empty> Hello(Empty request, ServerCallContext context)
            {
                return Task.FromResult(new Empty());
            }

            public override Task<P.CreateTaskHubResponse> CreateTaskHub(P.CreateTaskHubRequest request, ServerCallContext context)
            {
                this.GetDurabilityProvider(context).CreateAsync(request.RecreateIfExists);
                return Task.FromResult(new P.CreateTaskHubResponse());
            }

            public override Task<P.DeleteTaskHubResponse> DeleteTaskHub(P.DeleteTaskHubRequest request, ServerCallContext context)
            {
                this.GetDurabilityProvider(context).DeleteAsync();
                return Task.FromResult(new P.DeleteTaskHubResponse());
            }

            public async override Task<P.CreateInstanceResponse> StartInstance(P.CreateInstanceRequest request, ServerCallContext context)
            {
                try
                {
                    // Create the orchestration instance
                    var instance = new OrchestrationInstance
                    {
                        InstanceId = request.InstanceId ?? Guid.NewGuid().ToString("N"),
                        ExecutionId = Guid.NewGuid().ToString(),
                    };

                    // Create the ExecutionStartedEvent
                    ExecutionStartedEvent executionStartedEvent = new ExecutionStartedEvent(-1, request.Input)
                    {
                        Name = request.Name,
                        Version = request.Version,
                        OrchestrationInstance = instance,
                        ScheduledStartTime = request.ScheduledStartTimestamp?.ToDateTime(),
                    };

                    // Get the parent trace context from CreateInstanceRequest
                    string? traceParent = request.ParentTraceContext?.TraceParent;
                    string? traceState = request.ParentTraceContext?.TraceState;

                    // Create a new activity with the parent context
                    ActivityContext.TryParse(traceParent, traceState, out ActivityContext parentActivityContext);
                    using Activity? scheduleOrchestrationActivity = StartActivityForNewOrchestration(executionStartedEvent, parentActivityContext);

                    // Schedule the orchestration
                    await this.GetDurabilityProvider(context).CreateTaskOrchestrationAsync(
                        new TaskMessage
                        {
                            Event = executionStartedEvent,
                            OrchestrationInstance = instance,
                        },
                        this.GetStatusesNotToOverride());

                    return new P.CreateInstanceResponse
                    {
                        InstanceId = instance.InstanceId,
                    };
                }
                catch (OrchestrationAlreadyExistsException)
                {
                    throw new RpcException(new Status(StatusCode.AlreadyExists, $"An Orchestration instance with the ID {request.InstanceId} already exists."));
                }
                catch (InvalidOperationException ex) when (ex.Message.EndsWith("already exists.")) // for older versions of DTF.AS and DTFx.Netherite
                {
                    throw new RpcException(new Status(StatusCode.AlreadyExists, $"An Orchestration instance with the ID {request.InstanceId} already exists."));
                }
                catch (Exception ex)
                {
                    this.extension.TraceHelper.ExtensionWarningEvent(
                        this.extension.Options.HubName,
                        functionName: request.Name,
                        instanceId: request.InstanceId,
                        message: $"Failed to start instanceId {request.InstanceId} due to internal exception.\n Exception trace: {ex}.");
                    throw new RpcException(new Status(StatusCode.Internal, $"Failed to start instance with ID {request.InstanceId}.\nInner Exception message: {ex.Message}."));
                }
            }

            private OrchestrationStatus[] GetStatusesNotToOverride()
            {
                OverridableStates overridableStates = this.extension.Options.OverridableExistingInstanceStates;
                return overridableStates.ToDedupeStatuses();
            }

            internal static Activity? StartActivityForNewOrchestration(ExecutionStartedEvent startEvent, ActivityContext parentTraceContext)
            {
                // Create the Activity Source for the WebJobs extension
                ActivitySource activitySource = new ActivitySource("WebJobs.Extensions.DurableTask");

                // Start the new activity to represent scheduling the orchestration
                Activity? newActivity = activitySource.CreateActivity(
                    name: Schema.SpanNames.CreateOrchestration(startEvent.Name, startEvent.Version),
                    kind: ActivityKind.Producer,
                    parentContext: parentTraceContext);

                newActivity?.Start();

                if (newActivity != null && !string.IsNullOrEmpty(newActivity.Id))
                {
                    newActivity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
                    newActivity.SetTag(Schema.Task.Name, startEvent.Name);
                    newActivity.SetTag(Schema.Task.InstanceId, startEvent.OrchestrationInstance.InstanceId);
                    newActivity.SetTag(Schema.Task.ExecutionId, startEvent.OrchestrationInstance.ExecutionId);

                    if (!string.IsNullOrEmpty(startEvent.Version))
                    {
                        newActivity.SetTag(Schema.Task.Version, startEvent.Version);
                    }

                    // Set the parent trace context for the ExecutionStartedEvent
                    startEvent.ParentTraceContext = new DTCore.Tracing.DistributedTraceContext(newActivity?.Id!, newActivity?.TraceStateString);
                }

                return newActivity;
            }

            public async override Task<P.RaiseEventResponse> RaiseEvent(P.RaiseEventRequest request, ServerCallContext context)
            {
                bool throwStatusExceptionsOnRaiseEvent = this.extension.Options.ThrowStatusExceptionsOnRaiseEvent ?? this.extension.DefaultDurabilityProvider.CheckStatusBeforeRaiseEvent;

                try
                {
                    await this.GetClient(context).RaiseEventAsync(request.InstanceId, request.Name, Raw(request.Input));
                }
                catch (ArgumentNullException ex)
                {
                    // Indicates a required argument (e.g., instanceId, eventName, or input) is null or empty.
                    throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
                }
                catch (ArgumentException ex)
                {
                    // Indicates the provided instanceId has no orchestration status.
                    if (throwStatusExceptionsOnRaiseEvent)
                    {
                        throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
                    }
                }
                catch (InvalidOperationException)
                {
                    // Indicates the orchestration instance exists but is already in a completed state.
                    if (throwStatusExceptionsOnRaiseEvent)
                    {
                        throw new RpcException(new Status(StatusCode.FailedPrecondition, "The orchestration instance with the provided instance id is not running."));
                    }
                }
                catch (Exception ex)
                {
                    // Any other unexpected exceptions.
                    throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
                }

                return new P.RaiseEventResponse();
            }

            public async override Task<P.SignalEntityResponse> SignalEntity(P.SignalEntityRequest request, ServerCallContext context)
            {
                this.CheckEntitySupport(context, out var durabilityProvider, out var entityOrchestrationService);

                EntityMessageEvent eventToSend = ClientEntityHelpers.EmitOperationSignal(
                    new OrchestrationInstance() { InstanceId = request.InstanceId },
                    Guid.Parse(request.RequestId),
                    request.Name,
                    request.Input,
                    EntityMessageEvent.GetCappedScheduledTime(
                        DateTime.UtcNow,
                        entityOrchestrationService.EntityBackendProperties!.MaximumSignalDelayTime,
                        request.ScheduledTime?.ToDateTime()));

                await durabilityProvider.SendTaskOrchestrationMessageAsync(eventToSend.AsTaskMessage());

                // No fields in the response
                return new P.SignalEntityResponse();
            }

            public async override Task<P.GetEntityResponse> GetEntity(P.GetEntityRequest request, ServerCallContext context)
            {
                this.CheckEntitySupport(context, out var durabilityProvider, out var entityOrchestrationService);

                EntityBackendQueries.EntityMetadata? metaData = await entityOrchestrationService.EntityBackendQueries!.GetEntityAsync(
                    DTCore.Entities.EntityId.FromString(request.InstanceId),
                    request.IncludeState,
                    includeStateless: false,
                    context.CancellationToken);

                return new P.GetEntityResponse()
                {
                    Exists = metaData.HasValue,
                    Entity = metaData.HasValue ? this.ConvertEntityMetadata(metaData.Value) : default,
                };
            }

            public async override Task<P.QueryEntitiesResponse> QueryEntities(P.QueryEntitiesRequest request, ServerCallContext context)
            {
                this.CheckEntitySupport(context, out var durabilityProvider, out var entityOrchestrationService);

                P.EntityQuery query = request.Query;
                EntityBackendQueries.EntityQueryResult result = await entityOrchestrationService.EntityBackendQueries!.QueryEntitiesAsync(
                    new EntityBackendQueries.EntityQuery()
                    {
                        InstanceIdStartsWith = query.InstanceIdStartsWith,
                        LastModifiedFrom = query.LastModifiedFrom?.ToDateTime(),
                        LastModifiedTo = query.LastModifiedTo?.ToDateTime(),
                        IncludeTransient = query.IncludeTransient,
                        IncludeState = query.IncludeState,
                        ContinuationToken = query.ContinuationToken,
                        PageSize = query.PageSize,
                    },
                    context.CancellationToken);

                var response = new P.QueryEntitiesResponse()
                {
                    ContinuationToken = result.ContinuationToken,
                };

                foreach (EntityBackendQueries.EntityMetadata entityMetadata in result.Results)
                {
                    response.Entities.Add(this.ConvertEntityMetadata(entityMetadata));
                }

                return response;
            }

            public async override Task<P.CleanEntityStorageResponse> CleanEntityStorage(P.CleanEntityStorageRequest request, ServerCallContext context)
            {
                this.CheckEntitySupport(context, out var durabilityProvider, out var entityOrchestrationService);

                EntityBackendQueries.CleanEntityStorageResult result = await entityOrchestrationService.EntityBackendQueries!.CleanEntityStorageAsync(
                    new EntityBackendQueries.CleanEntityStorageRequest()
                    {
                        RemoveEmptyEntities = request.RemoveEmptyEntities,
                        ReleaseOrphanedLocks = request.ReleaseOrphanedLocks,
                        ContinuationToken = request.ContinuationToken,
                    },
                    context.CancellationToken);

                return new P.CleanEntityStorageResponse()
                {
                    EmptyEntitiesRemoved = result.EmptyEntitiesRemoved,
                    OrphanedLocksReleased = result.OrphanedLocksReleased,
                    ContinuationToken = result.ContinuationToken,
                };
            }

            public async override Task<P.TerminateResponse> TerminateInstance(P.TerminateRequest request, ServerCallContext context)
            {
                await this.GetClient(context).TerminateAsync(request.InstanceId, request.Output);
                return new P.TerminateResponse();
            }

            public async override Task<P.SuspendResponse> SuspendInstance(P.SuspendRequest request, ServerCallContext context)
            {
                await this.GetClient(context).SuspendAsync(request.InstanceId, request.Reason);
                return new P.SuspendResponse();
            }

            public async override Task<P.ResumeResponse> ResumeInstance(P.ResumeRequest request, ServerCallContext context)
            {
                await this.GetClient(context).ResumeAsync(request.InstanceId, request.Reason);
                return new P.ResumeResponse();
            }

            public async override Task<P.RewindInstanceResponse> RewindInstance(P.RewindInstanceRequest request, ServerCallContext context)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                await this.GetClient(context).RewindAsync(request.InstanceId, request.Reason);
#pragma warning restore CS0618 // Type or member is obsolete
                return new P.RewindInstanceResponse();
            }

            public async override Task<P.GetInstanceResponse> GetInstance(P.GetInstanceRequest request, ServerCallContext context)
            {
                OrchestrationState state = await this.GetDurabilityProvider(context)
                    .GetOrchestrationStateAsync(request.InstanceId, executionId: null);
                if (state == null)
                {
                    return new P.GetInstanceResponse() { Exists = false };
                }

                return CreateGetInstanceResponse(state, request);
            }

            public async override Task<P.QueryInstancesResponse> QueryInstances(P.QueryInstancesRequest request, ServerCallContext context)
            {
                var query = ProtobufUtils.ToOrchestrationQuery(request);
                var queryClient = (IOrchestrationServiceQueryClient)this.GetDurabilityProvider(context);
                OrchestrationQueryResult result = await queryClient.GetOrchestrationWithQueryAsync(query, context.CancellationToken);
                return ProtobufUtils.CreateQueryInstancesResponse(result, request);
            }

            public async override Task<P.PurgeInstancesResponse> PurgeInstances(P.PurgeInstancesRequest request, ServerCallContext context)
            {
                var purgeClient = (IOrchestrationServicePurgeClient)this.GetDurabilityProvider(context);

                PurgeResult result;
                switch (request.RequestCase)
                {
                    case P.PurgeInstancesRequest.RequestOneofCase.InstanceId:
                        result = await purgeClient.PurgeInstanceStateAsync(request.InstanceId);
                        break;

                    case P.PurgeInstancesRequest.RequestOneofCase.PurgeInstanceFilter:
                        var purgeInstanceFilter = ProtobufUtils.ToPurgeInstanceFilter(request);
                        result = await purgeClient.PurgeInstanceStateAsync(purgeInstanceFilter);
                        break;

                    default:
                        throw new ArgumentException($"Unknown purge request type '{request.RequestCase}'.");
                }

                return ProtobufUtils.CreatePurgeInstancesResponse(result);
            }

            public async override Task<P.GetInstanceResponse> WaitForInstanceStart(P.GetInstanceRequest request, ServerCallContext context)
            {
                int retryCount = 0;
                while (true)
                {
                    // Keep fetching the status until we get one of the states we care about
                    OrchestrationState state = await this.GetDurabilityProvider(context)
                        .GetOrchestrationStateAsync(request.InstanceId, executionId: null);
                    if (state != null && state.OrchestrationStatus != OrchestrationStatus.Pending)
                    {
                        return CreateGetInstanceResponse(state, request);
                    }

                    // Increase the delay time by 1 second every 10 seconds up to 10 seconds.
                    // The cancellation token is what will break us out of this loop if the orchestration
                    // never leaves the "Pending" state.
                    var delay = TimeSpan.FromSeconds(Math.Min(10, (retryCount / 10) + 1));
                    await Task.Delay(delay, context.CancellationToken);
                    retryCount++;
                }
            }

            public async override Task<P.GetInstanceResponse> WaitForInstanceCompletion(P.GetInstanceRequest request, ServerCallContext context)
            {
                OrchestrationState state = await this.GetDurabilityProvider(context).WaitForOrchestrationAsync(
                    request.InstanceId,
                    executionId: null,
                    timeout: Timeout.InfiniteTimeSpan,
                    context.CancellationToken);

                if (state == null)
                {
                    return new P.GetInstanceResponse() { Exists = false };
                }

                return CreateGetInstanceResponse(state, request);
            }

#pragma warning disable CS0618 // Type or member is obsolete -- 'internal' usage.
            private static RawInput Raw(string input)
            {
                return new RawInput(input);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            private static P.GetInstanceResponse CreateGetInstanceResponse(OrchestrationState state, P.GetInstanceRequest request)
            {
                return new P.GetInstanceResponse
                {
                    Exists = true,
                    OrchestrationState = new P.OrchestrationState
                    {
                        InstanceId = state.OrchestrationInstance.InstanceId,
                        Name = state.Name,
                        OrchestrationStatus = (P.OrchestrationStatus)state.OrchestrationStatus,
                        CreatedTimestamp = Timestamp.FromDateTime(state.CreatedTime),
                        LastUpdatedTimestamp = Timestamp.FromDateTime(state.LastUpdatedTime),
                        Input = request.GetInputsAndOutputs ? state.Input : null,
                        Output = request.GetInputsAndOutputs ? state.Output : null,
                        CustomStatus = request.GetInputsAndOutputs ? state.Status : null,
                        FailureDetails = request.GetInputsAndOutputs ? GetFailureDetails(state.FailureDetails) : null,
                    },
                };
            }

            private static P.TaskFailureDetails? GetFailureDetails(FailureDetails? failureDetails)
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
                };
            }

            private DurableClientAttribute GetAttribute(ServerCallContext context)
            {
                string? taskHub = context.RequestHeaders.GetValue("Durable-TaskHub");
                string? connectionName = context.RequestHeaders.GetValue("Durable-ConnectionName");
                return new DurableClientAttribute() { TaskHub = taskHub, ConnectionName = connectionName };
            }

            private DurabilityProvider GetDurabilityProvider(ServerCallContext context)
            {
                return this.extension.GetDurabilityProvider(this.GetAttribute(context));
            }

            private IDurableClient GetClient(ServerCallContext context)
            {
                return this.extension.GetClient(this.GetAttribute(context));
            }

            private void CheckEntitySupport(ServerCallContext context, out DurabilityProvider durabilityProvider, out IEntityOrchestrationService entityOrchestrationService)
            {
                durabilityProvider = this.GetDurabilityProvider(context);
                entityOrchestrationService = durabilityProvider;
                if (entityOrchestrationService?.EntityBackendProperties == null)
                {
                    throw new RpcException(new Grpc.Core.Status(
                        Grpc.Core.StatusCode.Unimplemented,
                        $"Missing entity support for storage backend '{durabilityProvider.GetBackendInfo()}'. Entity support" +
                        $" may have not been implemented yet, or the selected package version is too old."));
                }
            }

            private P.EntityMetadata ConvertEntityMetadata(EntityBackendQueries.EntityMetadata metaData)
            {
                return new P.EntityMetadata()
                {
                    InstanceId = metaData.EntityId.ToString(),
                    LastModifiedTime = metaData.LastModifiedTime.ToTimestamp(),
                    BacklogQueueSize = metaData.BacklogQueueSize,
                    LockedBy = metaData.LockedBy,
                    SerializedState = metaData.SerializedState,
                };
            }
        }
    }
}