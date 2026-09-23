// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.DurableTask.AzureBlobPayloads;
using Microsoft.DurableTask.Client;
using P = Microsoft.DurableTask.Protobuf.LargePayloads;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed class FunctionsLargePayloadPurgeClient(CallInvoker callInvoker) : ILargePayloadPurgeClient
{
    private readonly P.LargePayloadPurge.LargePayloadPurgeClient client = new(callInvoker);

    public async Task<List<LargePayloadTombstone>> GetLargePayloadTombstonesAsync(
        int limit, DateTime deadline, CancellationToken cancellationToken = default)
    {
        P.GetLargePayloadTombstonesResponse response = await this.client.GetLargePayloadTombstonesAsync(
            new P.GetLargePayloadTombstonesRequest { Limit = limit }, deadline: deadline, cancellationToken: cancellationToken);
        return response.Tombstones.Select(item => new LargePayloadTombstone(item.TombstoneToken, item.PayloadToken)).ToList();
    }

    public async Task ReportLargePayloadPurgeResultsAsync(
        IReadOnlyList<LargePayloadPurgeResult> results, DateTime deadline, CancellationToken cancellationToken = default)
    {
        if (results is null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        var request = new P.ReportLargePayloadPurgeResultsRequest();
        request.Results.AddRange(results.Select(result => new P.LargePayloadPurgeResult
        {
            TombstoneToken = result.TombstoneToken,
            Disposition = (P.LargePayloadPurgeDisposition)result.Disposition,
        }));
        await this.client.ReportLargePayloadPurgeResultsAsync(request, deadline: deadline, cancellationToken: cancellationToken);
    }
}
