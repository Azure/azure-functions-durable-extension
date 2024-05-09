// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
#if FUNCTIONS_V3_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class TaskHubGrpcServer : P.TaskHubSidecarService.TaskHubSidecarServiceBase
    {
        private readonly DurableTaskExtension extension;

        public TaskHubGrpcServer(DurableTaskExtension extension)
        {
            this.extension = extension;
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
            var instance = new OrchestrationInstance
            {
                InstanceId = request.InstanceId ?? Guid.NewGuid().ToString("N"),
                ExecutionId = Guid.NewGuid().ToString(),
            };

            await this.GetDurabilityProvider(context).CreateTaskOrchestrationAsync(
                new TaskMessage
                {
                    Event = new ExecutionStartedEvent(-1, request.Input)
                    {
                        Name = request.Name,
                        Version = request.Version,
                        OrchestrationInstance = instance,
                        ScheduledStartTime = request.ScheduledStartTimestamp?.ToDateTime(),
                    },
                    OrchestrationInstance = instance,
                });

            return new P.CreateInstanceResponse
            {
                InstanceId = instance.InstanceId,
            };
        }

        public async override Task<P.RaiseEventResponse> RaiseEvent(P.RaiseEventRequest request, ServerCallContext context)
        {
            await this.GetDurabilityProvider(context).SendTaskOrchestrationMessageAsync(
                new TaskMessage
                {
                    Event = new EventRaisedEvent(-1, request.Input)
                    {
                        Name = request.Name,
                    },
                    OrchestrationInstance = new OrchestrationInstance
                    {
                        InstanceId = request.InstanceId,
                    },
                });

            // No fields in the response
            return new P.RaiseEventResponse();
        }

        public async override Task<P.TerminateResponse> TerminateInstance(P.TerminateRequest request, ServerCallContext context)
        {
            await this.GetDurabilityProvider(context).ForceTerminateTaskOrchestrationAsync(
                request.InstanceId,
                request.Output);

            // No fields in the response
            return new P.TerminateResponse();
        }

        public async override Task<P.SuspendResponse> SuspendInstance(P.SuspendRequest request, ServerCallContext context)
        {
            await this.GetDurabilityProvider(context).SuspendTaskOrchestrationAsync(request.InstanceId, request.Reason);
            return new P.SuspendResponse();
        }

        public async override Task<P.ResumeResponse> ResumeInstance(P.ResumeRequest request, ServerCallContext context)
        {
            await this.GetDurabilityProvider(context).ResumeTaskOrchestrationAsync(request.InstanceId, request.Reason);
            return new P.ResumeResponse();
        }

        public async override Task<P.RewindInstanceResponse> RewindInstance(P.RewindInstanceRequest request, ServerCallContext context)
        {
            await this.GetDurabilityProvider(context).RewindAsync(request.InstanceId, request.Reason);
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

            return CreateGetInstanceResponse(state, request);
        }

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

        private DurabilityProvider GetDurabilityProvider(ServerCallContext context)
        {
            string? taskHub = context.RequestHeaders.GetValue("Durable-TaskHub");
            string? connectionName = context.RequestHeaders.GetValue("Durable-ConnectionName");
            var attribute = new DurableClientAttribute() { TaskHub = taskHub, ConnectionName = connectionName };
            return this.extension.GetDurabilityProvider(attribute);
        }
    }
}
#endif