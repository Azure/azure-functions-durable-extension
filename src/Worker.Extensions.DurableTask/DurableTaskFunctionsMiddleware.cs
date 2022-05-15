// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal class DurableTaskFunctionsMiddleware : IFunctionsWorkerMiddleware
{
    // TODO: Need to add call to action, i.e., pointer to some documentation.
    private const string IllegalAwaitErrorMessage =
        "An invalid asynchronous invocation was detected. This can be caused by awaiting non-durable tasks " +
        "in an orchestrator function's implementation or by middleware that invokes asynchronous code.";

    public async Task Invoke(FunctionContext functionContext, FunctionExecutionDelegate next)
    {
        if (!IsOrchestrationTrigger(functionContext, out BindingMetadata? triggerMetadata))
        {
            await next(functionContext);
            return;
        }

        InputBindingData<object> triggerInputData = await functionContext.BindInputAsync<object>(triggerMetadata);
        if (triggerInputData?.Value is not string encodedOrchestratorState)
        {
            throw new InvalidOperationException("Orchestration history state was either missing from the input or not a string value.");
        }

        // Wrap the function implementation in a lambda orchestrator
        string orchestratorOutput = OrchestrationRunner.LoadAndRun<object, object>(
            encodedOrchestratorState,
            async (orchestrationContext, input) =>
            {
                // Set the function input to be the orchestration context wrapped in our own object so that we can
                // intercept any of the calls and inject our own logic or tracking.
                FunctionsOrchestrationContext wrapperContext = new(orchestrationContext, functionContext);
                triggerInputData.Value = wrapperContext;

                // This method will advance to the next middleware and throw if it detects an asynchronous execution.
                await EnsureSynchronousExecution(functionContext, next, wrapperContext);

                // Set the raw function output as the orchestrator output
                object? functionOutput = functionContext.GetInvocationResult().Value;
                return functionOutput;
            },
            functionContext.InstanceServices);

        // Send the encoded orchestrator output as the return value seen by the functions host extension
        functionContext.GetInvocationResult().Value = orchestratorOutput;
    }

    private static bool IsOrchestrationTrigger(
        FunctionContext context,
        [NotNullWhen(true)] out BindingMetadata? orchestrationTriggerBinding)
    {
        foreach (BindingMetadata binding in context.FunctionDefinition.InputBindings.Values)
        {
            if (string.Equals(binding.Type, "orchestrationTrigger"))
            {
                orchestrationTriggerBinding = binding;
                return true;
            }
        }

        orchestrationTriggerBinding = null;
        return false;
    }

    private static async Task EnsureSynchronousExecution(
        FunctionContext functionContext,
        FunctionExecutionDelegate next,
        FunctionsOrchestrationContext orchestrationContext)
    {
        Task orchestratorTask = next(functionContext);
        if (!orchestratorTask.IsCompleted && !orchestrationContext.IsAccessed)
        {
            // If the middleware returns before the orchestrator function's context object was accessed, then we
            // know that either some middleware component went async or that the orchestrator function did some illegal
            // await as its very first action.
            throw new InvalidOperationException(IllegalAwaitErrorMessage);
        }

        await orchestratorTask;

        // This will throw if either the orchestrator performed an illegal await or if some middleware ahead of this
        // one performed some illegal await.
        orchestrationContext.ThrowIfIllegalAccess();
    }

    private sealed class FunctionsOrchestrationContext : TaskOrchestrationContext
    {
        private readonly TaskOrchestrationContext innerContext;
        private readonly FunctionContext functionContext;
        
        public FunctionsOrchestrationContext(TaskOrchestrationContext innerContext, FunctionContext functionContext)
        {
            this.innerContext = innerContext;
            this.functionContext = functionContext;
        }

        public bool IsAccessed { get; private set; }

        public override TaskName Name => this.innerContext.Name;

        public override string InstanceId => this.innerContext.InstanceId;

        public override DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

        public override bool IsReplaying => this.innerContext.IsReplaying;

        public override Task<T> CallActivityAsync<T>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            this.EnsureLegalAccess();
            return this.innerContext.CallActivityAsync<T>(name, input, options);
        }

        [Obsolete("Not yet supported")]
        public override Task<T> CallActivityAsync<T>(
            Func<object?, T> activityLambda,
            object? input = null,
            TaskOptions? options = null)
        {
            this.EnsureLegalAccess();
            return this.innerContext.CallActivityAsync(activityLambda, input, options);
        }

        public override Task<TResult> CallSubOrchestratorAsync<TResult>(
            TaskName orchestratorName,
            string? instanceId = null,
            object? input = null, 
            TaskOptions? options = null)
        {
            this.EnsureLegalAccess();
            return this.innerContext.CallSubOrchestratorAsync<TResult>(orchestratorName, instanceId, input, options);
        }

        public override void ContinueAsNew(object newInput, bool preserveUnprocessedEvents = true)
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
            this.SetCustomStatus(customStatus);
        }

        public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
        {
            this.EnsureLegalAccess();
            return this.innerContext.WaitForExternalEvent<T>(eventName, cancellationToken);
        }

        /// <summary>
        /// Throws if accessed by a non-orchestrator thread or marks the current object as accessed successfully.
        /// </summary>
        private void EnsureLegalAccess()
        {
            this.ThrowIfIllegalAccess();
            this.IsAccessed = true;
        }

        internal void ThrowIfIllegalAccess()
        {
            // Only the orchestrator thread is allowed to run the task continuation. If we detect that some other thread
            // got involved, it means that the orchestrator function (or some middleware that executed after it)
            // performed an await which scheduled a callback onto a worker pool thread, which isn't allowed. We throw
            // because the orchestrator is effectively stuck at this point.
            if (!global::DurableTask.Core.OrchestrationContext.IsOrchestratorThread)
            {
                InvalidOperationException exception = new(IllegalAwaitErrorMessage);

                // Log an error, since this exception will likely go unobserved.
                ILogger logger = this.functionContext.GetLogger<DurableTaskFunctionsMiddleware>();
                logger.LogError(exception, "The orchestrator function completed on a non-orchestrator thread!");
                throw exception;
            }
        }
    }
}