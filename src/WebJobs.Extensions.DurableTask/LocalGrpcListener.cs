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
                string? instanceId = null;
                try
                {
                    instanceId = await this.GetClient(context).StartNewAsync(request.Name, request.InstanceId, request.Input);
                    return new P.CreateInstanceResponse
                    {
                        InstanceId = instanceId,
                    };
                }
                catch (InvalidOperationException)
                {
                    throw new RpcException(new Status(StatusCode.AlreadyExists, $"An Orchestration instance with the ID {instanceId} already exists."));
                }
            }

            public async override Task<P.RaiseEventResponse> RaiseEvent(P.RaiseEventRequest request, ServerCallContext context)
            {
                await this.GetClient(context).RaiseEventAsync(request.InstanceId, request.Name, request.Input);
                return new P.RaiseEventResponse();
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
        }
    }
}
#endif