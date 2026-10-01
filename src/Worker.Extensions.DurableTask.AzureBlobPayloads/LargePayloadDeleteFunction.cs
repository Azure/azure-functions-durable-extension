// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.DurableTask.AzureBlobPayloads;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>The SDK deletion Function, activated with the configured payload store.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LargePayloadDeleteFunction(PayloadStore payloadStore, ILoggerFactory loggerFactory)
{
    /// <summary>Deletes external payloads using the configured shared SDK store.</summary>
    /// <param name="input">The payload tokens supplied by the orchestrator.</param>
    /// <param name="instanceId">The orchestration instance ID supplied by the activity binding.</param>
    /// <returns>The shared activity result.</returns>
    [Function(nameof(DeleteExternalBlobActivity))]
    public Task<List<BlobPurgeOutcome>> Delete([ActivityTrigger] List<string> input, string instanceId)
        => new DeleteExternalBlobActivity(payloadStore, loggerFactory.CreateLogger<DeleteExternalBlobActivity>())
            .RunAsync(new LargePayloadPurgeActivityContext(nameof(DeleteExternalBlobActivity), instanceId), input);
}
