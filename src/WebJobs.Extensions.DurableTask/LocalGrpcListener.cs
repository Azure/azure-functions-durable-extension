// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
#if FUNCTIONS_V3_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
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
        private readonly DurabilityProvider durabilityProvider;

        private readonly Random portGenerator;
        private readonly HashSet<int> attemptedPorts;

        private Server? grpcServer;

        public LocalGrpcListener(DurableTaskExtension extension, DurabilityProvider durabilityProvider)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
            this.durabilityProvider = durabilityProvider ?? throw new ArgumentNullException(nameof(durabilityProvider));

            this.portGenerator = new Random();
            this.attemptedPorts = new HashSet<int>();
        }

        public string? ListenAddress { get; private set; }

        public Task StartAsync(CancellationToken cancelToken = default)
        {
            const int maxAttempts = 10;
            int numAttempts = 1;
            while (numAttempts <= maxAttempts)
            {
                this.grpcServer = new Server();
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

                        return Task.CompletedTask;
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
            private readonly DurabilityProvider durabilityProvider;

            public TaskHubGrpcServer(LocalGrpcListener listener)
            {
                this.durabilityProvider = listener.durabilityProvider;
            }

            public override Task<Empty> Hello(Empty request, ServerCallContext context) => Task.FromResult(new Empty());

            public override Task<P.CreateTaskHubResponse> CreateTaskHub(P.CreateTaskHubRequest request, ServerCallContext context)
            {
                this.durabilityProvider.CreateAsync(request.RecreateIfExists);
                return Task.FromResult(new P.CreateTaskHubResponse());
            }

            public override Task<P.DeleteTaskHubResponse> DeleteTaskHub(P.DeleteTaskHubRequest request, ServerCallContext context)
            {
                this.durabilityProvider.DeleteAsync();
                return Task.FromResult(new P.DeleteTaskHubResponse());
            }

            public override async Task<P.CreateInstanceResponse> StartInstance(P.CreateInstanceRequest request, ServerCallContext context)
            {
                var instance = new OrchestrationInstance
                {
                    InstanceId = request.InstanceId ?? Guid.NewGuid().ToString("N"),
                    ExecutionId = Guid.NewGuid().ToString(),
                };

                await this.durabilityProvider.CreateTaskOrchestrationAsync(
                    new TaskMessage
                    {
                        Event = new ExecutionStartedEvent(-1, request.Input)
                        {
                            Name = request.Name,
                            Version = request.Version,
                            OrchestrationInstance = instance,
                        },
                        OrchestrationInstance = instance,
                    });

                return new P.CreateInstanceResponse
                {
                    InstanceId = instance.InstanceId,
                };
            }

            public override async Task<P.RaiseEventResponse> RaiseEvent(P.RaiseEventRequest request, ServerCallContext context)
            {
                await this.durabilityProvider.SendTaskOrchestrationMessageAsync(
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

            public override async Task<P.TerminateResponse> TerminateInstance(P.TerminateRequest request, ServerCallContext context)
            {
                await this.durabilityProvider.ForceTerminateTaskOrchestrationAsync(
                    request.InstanceId,
                    request.Output);

                // No fields in the response
                return new P.TerminateResponse();
            }

            public override async Task<P.SuspendResponse> SuspendInstance(P.SuspendRequest request, ServerCallContext context)
            {
                await this.durabilityProvider.SuspendTaskOrchestrationAsync(request.InstanceId, request.Reason);
                return new P.SuspendResponse();
            }

            public override async Task<P.ResumeResponse> ResumeInstance(P.ResumeRequest request, ServerCallContext context)
            {
                await this.durabilityProvider.ResumeTaskOrchestrationAsync(request.InstanceId, request.Reason);
                return new P.ResumeResponse();
            }

            public override async Task<P.RewindInstanceResponse> RewindInstance(P.RewindInstanceRequest request, ServerCallContext context)
            {
                await this.durabilityProvider.RewindAsync(request.InstanceId, request.Reason);
                return new P.RewindInstanceResponse();
            }

            public override async Task<P.GetInstanceResponse> GetInstance(P.GetInstanceRequest request, ServerCallContext context)
            {
                OrchestrationState state = await this.durabilityProvider.GetOrchestrationStateAsync(request.InstanceId, executionId: null);
                if (state == null)
                {
                    return new P.GetInstanceResponse() { Exists = false };
                }

                return CreateGetInstanceResponse(state, request);
            }

            public override async Task<P.QueryInstancesResponse> QueryInstances(P.QueryInstancesRequest request, ServerCallContext context)
            {
                OrchestrationQuery query = ProtobufUtils.ToOrchestrationQuery(request);
                var queryClient = (IOrchestrationServiceQueryClient)this.durabilityProvider;
                OrchestrationQueryResult result = await queryClient.GetOrchestrationWithQueryAsync(query, context.CancellationToken);
                return ProtobufUtils.CreateQueryInstancesResponse(result, request);
            }

            public override async Task<P.PurgeInstancesResponse> PurgeInstances(P.PurgeInstancesRequest request, ServerCallContext context)
            {
                var purgeClient = (IOrchestrationServicePurgeClient)this.durabilityProvider;

                PurgeResult result;
                switch (request.RequestCase)
                {
                    case P.PurgeInstancesRequest.RequestOneofCase.InstanceId:
                        result = await purgeClient.PurgeInstanceStateAsync(request.InstanceId);
                        break;

                    case P.PurgeInstancesRequest.RequestOneofCase.PurgeInstanceFilter:
                        PurgeInstanceFilter purgeInstanceFilter = ProtobufUtils.ToPurgeInstanceFilter(request);
                        result = await purgeClient.PurgeInstanceStateAsync(purgeInstanceFilter);
                        break;

                    default:
                        throw new ArgumentException($"Unknown purge request type '{request.RequestCase}'.");
                }

                return ProtobufUtils.CreatePurgeInstancesResponse(result);
            }

            public override async Task<P.GetInstanceResponse> WaitForInstanceStart(P.GetInstanceRequest request, ServerCallContext context)
            {
                int retryCount = 0;
                while (true)
                {
                    // Keep fetching the status until we get one of the states we care about
                    OrchestrationState state = await this.durabilityProvider.GetOrchestrationStateAsync(request.InstanceId, executionId: null);
                    if (state != null && state.OrchestrationStatus != OrchestrationStatus.Pending)
                    {
                        return CreateGetInstanceResponse(state, request);
                    }

                    // Increase the delay time by 1 second every 10 seconds up to 10 seconds.
                    // The cancellation token is what will break us out of this loop if the orchestration
                    // never leaves the "Pending" state.
                    TimeSpan delay = TimeSpan.FromSeconds(Math.Min(10, (retryCount / 10) + 1));
                    await Task.Delay(delay, context.CancellationToken);
                    retryCount++;
                }
            }

            public override async Task<P.GetInstanceResponse> WaitForInstanceCompletion(P.GetInstanceRequest request, ServerCallContext context)
            {
                OrchestrationState state = await this.durabilityProvider.WaitForOrchestrationAsync(
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
        }
    }
}
#endif