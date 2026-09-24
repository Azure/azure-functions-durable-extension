// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Core;
using Grpc.Core;
using LP = Microsoft.DurableTask.Protobuf.LargePayloads;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class LargePayloadPurgeGrpcServer : LP.LargePayloadPurge.LargePayloadPurgeBase
    {
        private readonly TaskHubGrpcServer taskHubServer;

        public LargePayloadPurgeGrpcServer(DurableTaskExtension extension)
        {
            this.taskHubServer = new TaskHubGrpcServer(extension);
        }

        public async override Task<LP.SetLargePayloadAutoPurgeResponse> SetLargePayloadAutoPurge(LP.SetLargePayloadAutoPurgeRequest request, ServerCallContext context)
        {
            var purgeClient = (IOrchestrationServiceLargePayloadPurgeClient)this.taskHubServer.GetDurabilityProvider(context);
            try
            {
                // This records a setting only. Purge Functions execute in the language worker.
                await purgeClient.SetLargePayloadAutoPurgeAsync(request.Enabled, context.Deadline, context.CancellationToken);
            }
            catch (NotSupportedException exception)
            {
                throw new RpcException(new Status(StatusCode.Unimplemented, exception.Message));
            }

            return new LP.SetLargePayloadAutoPurgeResponse();
        }

        public async override Task<LP.GetLargePayloadTombstonesResponse> GetLargePayloadTombstones(LP.GetLargePayloadTombstonesRequest request, ServerCallContext context)
        {
            var purgeClient = (IOrchestrationServiceLargePayloadPurgeClient)this.taskHubServer.GetDurabilityProvider(context);
            IReadOnlyList<LargePayloadPurgeTombstone> tombstones;
            try
            {
                tombstones = await purgeClient.GetLargePayloadsToPurgeAsync(request.Limit, context.Deadline, context.CancellationToken);
            }
            catch (NotSupportedException exception)
            {
                throw new RpcException(new Status(StatusCode.Unimplemented, exception.Message));
            }

            var response = new LP.GetLargePayloadTombstonesResponse();
            foreach (LargePayloadPurgeTombstone tombstone in tombstones)
            {
                response.Tombstones.Add(new LP.LargePayloadTombstone
                {
                    TombstoneToken = tombstone.TombstoneToken,
                    PayloadToken = tombstone.PayloadToken,
                });
            }

            return response;
        }

        public async override Task<LP.ReportLargePayloadPurgeResultsResponse> ReportLargePayloadPurgeResults(LP.ReportLargePayloadPurgeResultsRequest request, ServerCallContext context)
        {
            var purgeClient = (IOrchestrationServiceLargePayloadPurgeClient)this.taskHubServer.GetDurabilityProvider(context);
            var results = new List<LargePayloadPurgeResult>(request.Results.Count);
            foreach (LP.LargePayloadPurgeResult result in request.Results)
            {
                // Preserve even unspecified or unknown values: validation belongs to the provider.
                results.Add(new LargePayloadPurgeResult(
                    result.TombstoneToken,
                    (LargePayloadPurgeDisposition)result.Disposition));
            }

            try
            {
                await purgeClient.ReportLargePayloadPurgeResultsAsync(results, context.Deadline, context.CancellationToken);
            }
            catch (NotSupportedException exception)
            {
                throw new RpcException(new Status(StatusCode.Unimplemented, exception.Message));
            }

            return new LP.ReportLargePayloadPurgeResultsResponse();
        }
    }
}
