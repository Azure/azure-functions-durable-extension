// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core.History;
using Microsoft.DurableTask;
using Microsoft.DurableTask.AzureBlobPayloads;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Client.Grpc.Internal;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// A <b>per-binding</b> wrapper around an existing <see cref="DurableTaskClient" />. This is used to
/// hold onto some binding-specific details for functions-specific features and behavior.
/// </summary>
internal sealed class FunctionsDurableTaskClient : DurableTaskClient, ILargePayloadAutoPurgeClient, ILargePayloadPurgeClient
{
    private readonly DurableTaskClient inner;
    private readonly ILargePayloadPurgeClient? purgeClient;

    public FunctionsDurableTaskClient(DurableTaskClient inner, string? queryString, string? httpBaseUrl, ILargePayloadPurgeClient? purgeClient = null)
        : base(inner.Name)
    {
        this.inner = inner;
        this.purgeClient = purgeClient;
        this.QueryString = queryString;
        this.HttpBaseUrl = httpBaseUrl;
    }

    public string? QueryString { get; }
    public string? HttpBaseUrl { get; }
    public override DurableEntityClient Entities => this.inner.Entities;

    Task ILargePayloadAutoPurgeClient.SetLargePayloadAutoPurgeAsync(bool enabled, CancellationToken cancellation)
        => this.inner is ILargePayloadAutoPurgeClient control
            ? control.SetLargePayloadAutoPurgeAsync(enabled, cancellation)
            : throw new NotSupportedException("The bound Durable Functions client does not support large payload auto-purge.");

    Task<List<LargePayloadTombstone>> ILargePayloadPurgeClient.GetLargePayloadTombstonesAsync(int limit, DateTime deadline, CancellationToken cancellationToken)
        => this.GetPurgeClient().GetLargePayloadTombstonesAsync(limit, deadline, cancellationToken);

    Task ILargePayloadPurgeClient.ReportLargePayloadPurgeResultsAsync(IReadOnlyList<LargePayloadPurgeResult> results, DateTime deadline, CancellationToken cancellationToken)
        => this.GetPurgeClient().ReportLargePayloadPurgeResultsAsync(results, deadline, cancellationToken);

    private ILargePayloadPurgeClient GetPurgeClient()
        => this.purgeClient ?? throw new NotSupportedException("The bound Durable Functions client does not support large payload purge operations.");

    public override ValueTask DisposeAsync()
    {
        // We do not dispose inner client as it has a longer life than this class.
        return default;
    }

    public override AsyncPageable<OrchestrationMetadata> GetAllInstancesAsync(OrchestrationQuery? filter = null)
    {
        return this.inner.GetAllInstancesAsync(filter);
    }

    public override Task<OrchestrationMetadata?> GetInstancesAsync(
        string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        return this.inner.GetInstancesAsync(instanceId, getInputsAndOutputs, cancellation);
    }

    public override Task<PurgeResult> PurgeAllInstancesAsync(
        PurgeInstancesFilter filter, PurgeInstanceOptions? options = null, CancellationToken cancellation = default)
    {
        return this.inner.PurgeAllInstancesAsync(filter, options, cancellation);
    }

    public override Task<PurgeResult> PurgeInstanceAsync(
        string instanceId, PurgeInstanceOptions? options = null, CancellationToken cancellation = default)
    {
        return this.inner.PurgeInstanceAsync(instanceId, options, cancellation);
    }

    public override Task RaiseEventAsync(
        string instanceId, string eventName, object? eventPayload = null, CancellationToken cancellation = default)
    {
        return this.inner.RaiseEventAsync(instanceId, eventName, eventPayload, cancellation);
    }

    public override Task ResumeInstanceAsync(
        string instanceId, string? reason = null, CancellationToken cancellation = default)
    {
        return this.inner.ResumeInstanceAsync(instanceId, reason, cancellation);
    }

    public override Task<string> ScheduleNewOrchestrationInstanceAsync(
        TaskName orchestratorName,
        object? input = null,
        StartOrchestrationOptions? options = null,
        CancellationToken cancellation = default)
    {
        return this.inner.ScheduleNewOrchestrationInstanceAsync(orchestratorName, input, options, cancellation);
    }

    public override Task SuspendInstanceAsync(
        string instanceId, string? reason = null, CancellationToken cancellation = default)
    {
        return this.inner.SuspendInstanceAsync(instanceId, reason, cancellation);
    }

    public override Task TerminateInstanceAsync(
        string instanceId, TerminateInstanceOptions? options = null, CancellationToken cancellation = default)
    {
        return this.inner.TerminateInstanceAsync(instanceId, options, cancellation);
    }

    public override Task<OrchestrationMetadata> WaitForInstanceCompletionAsync(
        string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        return this.inner.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs, cancellation);
    }

    public override Task<OrchestrationMetadata> WaitForInstanceStartAsync(
        string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        return this.inner.WaitForInstanceStartAsync(instanceId, getInputsAndOutputs, cancellation);
    }

    public override Task<string> RestartAsync(
        string instanceId, bool restartWithNewInstanceId = false,CancellationToken cancellation = default)
    {
        return this.inner.RestartAsync(instanceId, restartWithNewInstanceId, cancellation);
    }

    public override Task RewindInstanceAsync(
        string instanceId, string reason, CancellationToken cancellation = default)
    {
        return this.inner.RewindInstanceAsync(instanceId, reason, cancellation);
    }

    public override Task<IList<HistoryEvent>> GetOrchestrationHistoryAsync(
        string instanceId,
        CancellationToken cancellation = default)
    {
        return this.inner.GetOrchestrationHistoryAsync(instanceId, cancellation);
    }
}
