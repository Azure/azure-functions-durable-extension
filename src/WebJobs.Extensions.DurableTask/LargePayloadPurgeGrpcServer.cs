// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
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
            // This records a setting only. Purge Functions execute in the language worker.
            await this.GetLargePayloadPurgeProvider(context).SetLargePayloadAutoPurgeAsync(
                request.Enabled,
                context.Deadline,
                context.CancellationToken);
            return new LP.SetLargePayloadAutoPurgeResponse();
        }

        public async override Task<LP.GetLargePayloadTombstonesResponse> GetLargePayloadTombstones(LP.GetLargePayloadTombstonesRequest request, ServerCallContext context)
        {
            IReadOnlyList<LargePayloadPurgeTombstone> tombstones = await this.GetLargePayloadPurgeProvider(context).GetLargePayloadsToPurgeAsync(
                request.Limit,
                context.Deadline,
                context.CancellationToken);
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
            ILargePayloadPurgeProvider provider = this.GetLargePayloadPurgeProvider(context);
            var results = new List<LargePayloadPurgeResult>(request.Results.Count);
            foreach (LP.LargePayloadPurgeResult result in request.Results)
            {
                // Preserve even unspecified or unknown values: validation belongs to the provider.
                results.Add(new LargePayloadPurgeResult(
                    result.TombstoneToken,
                    (LargePayloadPurgeDisposition)result.Disposition));
            }

            await provider.ReportLargePayloadPurgeResultsAsync(results, context.Deadline, context.CancellationToken);
            return new LP.ReportLargePayloadPurgeResultsResponse();
        }

        private ILargePayloadPurgeProvider GetLargePayloadPurgeProvider(ServerCallContext context)
        {
            if (this.taskHubServer.GetDurabilityProvider(context) is ILargePayloadPurgeProvider provider)
            {
                return provider;
            }

            throw new RpcException(new Status(StatusCode.Unimplemented, "The selected durability provider does not support large-payload purge."));
        }
    }
}
