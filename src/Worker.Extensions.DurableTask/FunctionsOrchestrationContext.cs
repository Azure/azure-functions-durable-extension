// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed partial class FunctionsOrchestrationContext : TaskOrchestrationContext
{
    private readonly TaskOrchestrationContext innerContext;
    private readonly FunctionContext functionContext;
    private readonly JsonSerializerOptions jsonOptions;

    private readonly DurableTaskWorkerOptions options;

    private InputConverter? inputConverter;
    private EntityFeature? entities;

    public FunctionsOrchestrationContext(TaskOrchestrationContext innerContext, FunctionContext functionContext)
    {
        this.innerContext = innerContext;
        this.functionContext = functionContext;
        this.options = this.functionContext.InstanceServices
            .GetRequiredService<IOptions<DurableTaskWorkerOptions>>().Value;
        this.jsonOptions = functionContext.InstanceServices
            .GetRequiredService<IOptions<JsonSerializerOptions>>().Value;
        this.LoggerFactory = functionContext.InstanceServices.GetRequiredService<ILoggerFactory>();
    }

    public bool IsAccessed { get; private set; }

    public override TaskName Name => this.innerContext.Name;

    public override string InstanceId => this.innerContext.InstanceId;

    public override DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

    public override bool IsReplaying => this.innerContext.IsReplaying;

    public override ParentOrchestrationInstance? Parent => this.innerContext.Parent;

    public override string Version => this.innerContext.Version;

    public override IReadOnlyDictionary<string, object?> Properties => this.innerContext.Properties;

    protected override ILoggerFactory LoggerFactory { get; }

    public override TaskOrchestrationEntityFeature Entities =>
        this.entities ??= new EntityFeature(this, this.innerContext.Entities);

    public override T GetInput<T>()
    {
        this.EnsureLegalAccess();

        object? input = this.innerContext.GetInput<object>();
        if (input is T typed)
        {
            return typed;
        }

        // The wrapped TaskOrchestrationContext is not actually dynamic with GetInput - it was set
        // once based on the declared input type of the orchestrator. Since we do not know the
        // desired input type upfront, we were initialized to object. So we must serialize and
        // deserialize again to convert to our desired type.
        this.inputConverter ??= InputConverter.Create(input, this.options.DataConverter, this.jsonOptions);
        return this.inputConverter.Get<T>();
    }

    public override Guid NewGuid()
    {
        this.EnsureLegalAccess();
        return this.innerContext.NewGuid();
    }

    public override Task<T> CallActivityAsync<T>(TaskName name, object? input = null, TaskOptions? options = null)
    {
        this.EnsureLegalAccess();
        return this.innerContext.CallActivityAsync<T>(name, input, options);
    }

    public override Task<TResult> CallSubOrchestratorAsync<TResult>(
        TaskName orchestratorName, object? input = null, TaskOptions? options = null)
    {
        this.EnsureLegalAccess();
        // If we have a default version, add it to the TaskOptions and call the inner context with it.
        if (this.innerContext.Properties.TryGetValue("defaultVersion", out var propVersion) && propVersion is string defaultVersion)
        {
            // TODO: Update to copy constructors when available - https://github.com/microsoft/durabletask-dotnet/issues/440
            SubOrchestrationOptions subOptions;
            if (options is SubOrchestrationOptions subOrchestrationOptions)
            {
                subOptions = new SubOrchestrationOptions(options)
                {
                    InstanceId = subOrchestrationOptions.InstanceId,
                    Version = subOrchestrationOptions.Version?.Version ?? defaultVersion,
                };
            }
            else
            {
                subOptions = new SubOrchestrationOptions(options ?? new TaskOptions())
                {
                    Version = defaultVersion
                };
            }
            return this.innerContext.CallSubOrchestratorAsync<TResult>(orchestratorName, input, subOptions);
        }

        return this.innerContext.CallSubOrchestratorAsync<TResult>(orchestratorName, input, options);
    }

    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
    {
        this.EnsureLegalAccess();
        this.innerContext.ContinueAsNew(newInput, preserveUnprocessedEvents);
    }

    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        this.EnsureLegalAccess();
        return this.innerContext.CreateTimer(fireAt, cancellationToken);
    }

    public override void SetCustomStatus(object? customStatus)
    {
        this.EnsureLegalAccess();
        this.innerContext.SetCustomStatus(customStatus);
    }

    public override void SendEvent(string instanceId, string eventName, object payload)
    {
        this.EnsureLegalAccess();
        this.innerContext.SendEvent(instanceId, eventName, payload);
    }

    public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
    {
        this.EnsureLegalAccess();
        return this.innerContext.WaitForExternalEvent<T>(eventName, cancellationToken);
    }

    internal void ThrowIfIllegalAccess()
    {
        // Only the orchestrator thread is allowed to run the task continuation. If we detect that some other thread
        // got involved, it means that the orchestrator function (or some middleware that executed after it)
        // performed an await which scheduled a callback onto a worker pool thread, which isn't allowed. We throw
        // because the orchestrator is effectively stuck at this point.
        if (!global::DurableTask.Core.OrchestrationContext.IsOrchestratorThread)
        {
            try
            {
                throw new InvalidOperationException(Constants.IllegalAwaitErrorMessage);
            }
            catch (Exception ex) // stack not set until after a 'throw' statement.
            {
                // Log an error, since this exception will likely go unobserved.
                ILogger logger = this.functionContext.GetLogger<DurableTaskFunctionsMiddleware>();
                logger.LogError(ex, "The orchestrator function completed on a non-orchestrator thread!");
                throw;
            }
        }
    }

    /// <summary>
    /// Throws if accessed by a non-orchestrator thread or marks the current object as accessed successfully.
    /// </summary>
    private void EnsureLegalAccess()
    {
        this.ThrowIfIllegalAccess();
        this.IsAccessed = true;
    }
}
