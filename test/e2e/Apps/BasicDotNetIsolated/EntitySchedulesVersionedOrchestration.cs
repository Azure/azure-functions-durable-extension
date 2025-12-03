// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

/// <summary>
/// Tests Entity-scheduled orchestrations should use the host's defaultVersion.
/// This file contains entities that schedule orchestrations and return version information
/// to verify that the defaultVersion from host.json is correctly applied.
/// </summary>
public static class EntitySchedulesVersionedOrchestration
{
    /// <summary>
    /// HTTP trigger to start the test orchestration that calls an entity which schedules a versioned orchestration.
    /// </summary>
    [Function("EntitySchedulesVersionedOrchestration_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string? explicitVersion)
    {
        ILogger logger = executionContext.GetLogger("EntitySchedulesVersionedOrchestration_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(EntitySchedulesVersionedOrchestrationOrchestrator),
            input: explicitVersion);

        logger.LogInformation(
            "Started EntitySchedulesVersionedOrchestration with ID = '{instanceId}', explicitVersion = '{explicitVersion}'.",
            instanceId, explicitVersion);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    /// <summary>
    /// Orchestrator that calls an entity to schedule another orchestration.
    /// The entity returns the version of the scheduled orchestration.
    /// </summary>
    [Function(nameof(EntitySchedulesVersionedOrchestrationOrchestrator))]
    public static async Task<string> EntitySchedulesVersionedOrchestrationOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string? explicitVersion = context.GetInput<string?>();

        var entityId = new EntityInstanceId(nameof(VersionSchedulerEntity), "singleton");
        var scheduleRequest = new ScheduleOrchestrationRequest
        {
            ExplicitVersion = explicitVersion,
        };

        // Call the entity which will schedule an orchestration and return the scheduled instance ID
        string scheduledInstanceId = await context.Entities.CallEntityAsync<string>(
            entityId,
            nameof(VersionSchedulerEntity.ScheduleOrchestration),
            scheduleRequest);

        // Wait a bit for the scheduled orchestration to complete, then get its output
        // We use a sub-orchestrator to fetch the result
        string result = await context.CallSubOrchestratorAsync<string>(
            nameof(WaitForScheduledOrchestrationResult),
            scheduledInstanceId);

        return result;
    }

    /// <summary>
    /// Sub-orchestrator that waits for a scheduled orchestration to complete and returns its output.
    /// </summary>
    [Function(nameof(WaitForScheduledOrchestrationResult))]
    public static async Task<string> WaitForScheduledOrchestrationResult(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string instanceId = context.GetInput<string>()!;

        // Poll for the result using an activity
        return await context.CallActivityAsync<string>(
            nameof(GetOrchestrationResultActivity),
            instanceId);
    }

    /// <summary>
    /// Activity that polls for an orchestration result.
    /// </summary>
    [Function(nameof(GetOrchestrationResultActivity))]
    public static async Task<string> GetOrchestrationResultActivity(
        [ActivityTrigger] string instanceId,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(GetOrchestrationResultActivity));

        // Poll for completion (max 30 seconds)
        for (int i = 0; i < 60; i++)
        {
            var metadata = await client.GetInstancesAsync(instanceId, getInputsAndOutputs: true);
            if (metadata?.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
            {
                var result = metadata.ReadOutputAs<string>();
                logger.LogInformation("Scheduled orchestration '{instanceId}' completed with output: {result}", instanceId, result);
                return result ?? "null";
            }
            if (metadata?.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
            {
                logger.LogError("Scheduled orchestration '{instanceId}' failed", instanceId);
                return $"FAILED: {instanceId}";
            }
            await Task.Delay(500);
        }

        return $"TIMEOUT: {instanceId}";
    }

    /// <summary>
    /// The orchestration that gets scheduled by the entity.
    /// It returns its version information.
    /// </summary>
    [Function(nameof(EntityScheduledVersionedOrchestrator))]
    public static string EntityScheduledVersionedOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return $"EntityScheduledVersion: '{context.Version}'";
    }
}

/// <summary>
/// Entity that schedules orchestrations with or without explicit versions.
/// </summary>
public class VersionSchedulerEntity : TaskEntity<string?>
{
    /// <summary>
    /// Schedules a new orchestration. If explicitVersion is null or empty,
    /// the orchestration should receive the host's defaultVersion.
    /// </summary>
    public string ScheduleOrchestration(ScheduleOrchestrationRequest request)
    {
        string? explicitVersion = request?.ExplicitVersion;
        StartOrchestrationOptions options = string.IsNullOrWhiteSpace(explicitVersion)
            ? new StartOrchestrationOptions()
            : new StartOrchestrationOptions { Version = explicitVersion };

        string instanceId = this.Context.ScheduleNewOrchestration(
            nameof(EntitySchedulesVersionedOrchestration.EntityScheduledVersionedOrchestrator),
            input: null,
            options);

        return instanceId;
    }

    protected override string? InitializeState(TaskEntityOperation operation)
    {
        return null;
    }

    [Function(nameof(VersionSchedulerEntity))]
    public Task RunEntityAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync(this);
    }
}

/// <summary>
/// Defines the payload sent to the VersionScheduler entity so that null versions are serialized explicitly.
/// </summary>
public sealed class ScheduleOrchestrationRequest
{
    public string? ExplicitVersion { get; init; }
}
