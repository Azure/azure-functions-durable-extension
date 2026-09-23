// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.DurableTask.AzureBlobPayloads;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// SDK-owned Functions invoked by the isolated worker for external payload cleanup.
/// Referencing the optional package supplies these ordinary Functions.
/// <see cref="LargePayloadPurgeFunctionsExtensions.ConfigureLargePayloadPurgeFunctions"/> configures their payload store.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LargePayloadPurgeFunctions(PayloadStore payloadStore, ILoggerFactory loggerFactory)
{
    /// <summary>Executes the shared SDK purge orchestrator as an ordinary Function.</summary>
    /// <param name="context">The bound orchestration context.</param>
    /// <returns>The shared orchestrator result.</returns>
    [Function(nameof(BlobPurgeJobOrchestrator))]
    public static Task<object?> RunOrchestrator([OrchestrationTrigger(LargePayloadPurge = true)] TaskOrchestrationContext context)
        => new BlobPurgeJobOrchestrator().RunAsync(context, context.GetInput<BlobPurgeJobRunRequest>()!);

    /// <summary>Fetches tombstones through the current worker's task hub client binding.</summary>
    /// <param name="input">The batch size.</param>
    /// <param name="instanceId">The orchestration instance ID supplied by the activity binding.</param>
    /// <param name="client">The bound local Functions client.</param>
    /// <returns>The shared activity result.</returns>
    [Function(nameof(GetLargePayloadTombstonesActivity))]
    public Task<List<LargePayloadTombstone>> Fetch(
        [ActivityTrigger] int input, string instanceId, [DurableClient] DurableTaskClient client)
        => new GetLargePayloadTombstonesActivity(GetPurgeClient(client), loggerFactory.CreateLogger<GetLargePayloadTombstonesActivity>())
            .RunAsync(new ActivityContext(nameof(GetLargePayloadTombstonesActivity), instanceId), input);

    /// <summary>Deletes external payloads using the configured shared SDK store.</summary>
    /// <param name="input">The payload tokens supplied by the orchestrator.</param>
    /// <param name="instanceId">The orchestration instance ID supplied by the activity binding.</param>
    /// <returns>The shared activity result.</returns>
    [Function(nameof(DeleteExternalBlobActivity))]
    public Task<List<BlobPurgeOutcome>> Delete([ActivityTrigger] List<string> input, string instanceId)
        => new DeleteExternalBlobActivity(payloadStore, loggerFactory.CreateLogger<DeleteExternalBlobActivity>())
            .RunAsync(new ActivityContext(nameof(DeleteExternalBlobActivity), instanceId), input);

    /// <summary>Reports outcomes through the current worker's task hub client binding.</summary>
    /// <param name="input">The deletion outcomes.</param>
    /// <param name="instanceId">The orchestration instance ID supplied by the activity binding.</param>
    /// <param name="client">The bound local Functions client.</param>
    /// <returns>The shared activity result.</returns>
    [Function(nameof(ReportLargePayloadPurgeResultsActivity))]
    public Task<object?> Report(
        [ActivityTrigger] List<LargePayloadPurgeResult> input, string instanceId, [DurableClient] DurableTaskClient client)
        => new ReportLargePayloadPurgeResultsActivity(GetPurgeClient(client), loggerFactory.CreateLogger<ReportLargePayloadPurgeResultsActivity>())
            .RunAsync(new ActivityContext(nameof(ReportLargePayloadPurgeResultsActivity), instanceId), input);

    private static ILargePayloadPurgeClient GetPurgeClient(DurableTaskClient client)
        => client as ILargePayloadPurgeClient
            ?? throw new NotSupportedException("The bound Durable Functions client does not support large payload purge operations.");

    private sealed class ActivityContext(TaskName name, string instanceId) : TaskActivityContext
    {
        public override TaskName Name { get; } = name;

        public override string InstanceId { get; } = instanceId;
    }
}
