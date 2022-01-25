// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OutOfProcMiddleware
    {
        private readonly DurableTaskExtension extension;

        public OutOfProcMiddleware(DurableTaskExtension extension)
        {
            this.extension = extension;
        }

        // The below private properties are just passthroughs to properties or methods defined in the extension class.
        // They exist simply to make it easier to copy/paste logic from the old middleware defined there to this file.

        private DurableTaskOptions Options => this.extension.Options;

        private EndToEndTraceHelper TraceHelper => this.extension.TraceHelper;

        private ILifeCycleNotificationHelper LifeCycleNotificationHelper => this.extension.LifeCycleNotificationHelper;

        private IApplicationLifetimeWrapper HostLifetimeService => this.extension.HostLifetimeService;

        /// <summary>
        /// Durable Task Framework orchestration middleware that invokes an out-of-process orchestrator function.
        /// </summary>
        /// <param name="dispatchContext">This middleware context provided by the framework that contains information about the orchestration.</param>
        /// <param name="next">The next middleware handler in the pipeline.</param>
        /// <exception cref="InvalidOperationException">Thrown if there's an internal failure in the middleware.</exception>
        /// <exception cref="SessionAbortedException">Thrown if there is a recoverable error in the Functions runtime that's expected to be handled gracefully.</exception>
        public async Task CallOrchestratorAsync(DispatchMiddlewareContext dispatchContext, Func<Task> next)
        {
            OrchestrationRuntimeState? runtimeState = dispatchContext.GetProperty<OrchestrationRuntimeState>();
            if (runtimeState == null)
            {
                // This should never happen, but it's almost certainly non-retriable if it does.
                dispatchContext.SetProperty(OrchestratorExecutionResult.ForFailure(
                    message: "Orchestration runtime state was missing!",
                    details: null));
                return;
            }

            OrchestrationInstance? instance = dispatchContext.GetProperty<OrchestrationInstance>();
            if (instance == null)
            {
                // This should never happen, but it's almost certainly non-retriable if it does.
                dispatchContext.SetProperty(OrchestratorExecutionResult.ForFailure(
                    message: "Instance ID metadata was missing!",
                    details: null));
                return;
            }

            FunctionName functionName = new FunctionName(runtimeState.Name);
            RegisteredFunctionInfo? function = this.extension.GetOrchestratorInfo(functionName);
            if (function == null)
            {
                // Fail the orchestration with an error explaining that the function name is invalid.
                string errorMessage = this.extension.GetInvalidOrchestratorFunctionMessage(functionName.Name);
                dispatchContext.SetProperty(OrchestratorExecutionResult.ForFailure(errorMessage, details: null));
                return;
            }

            ExecutionStartedEvent? startEvent = runtimeState.ExecutionStartedEvent;
            if (startEvent == null)
            {
                // This should never happen, but it's almost certainly non-retriable if it does.
                dispatchContext.SetProperty(OrchestratorExecutionResult.ForFailure(
                    message: "ExecutionStartedEvent was missing from runtime state!",
                    details: null));
                return;
            }

            bool isReplaying = runtimeState.PastEvents.Any();

            this.TraceHelper.FunctionStarting(
                this.Options.HubName,
                functionName.Name,
                instance.InstanceId,
                isReplaying ? "(replay)" : this.extension.GetIntputOutputTrace(startEvent.Input),
                FunctionType.Orchestrator,
                isReplaying);

            // One-time logging/notifications for when the orchestration first starts.
            if (!isReplaying)
            {
#if !FUNCTIONS_V1
                DurableTaskExtension.TagActivityWithOrchestrationStatus(OrchestrationRuntimeStatus.Running, instance.InstanceId);
#endif
                await this.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    isReplay: false);
            }

            var context = new RemoteOrchestratorContext(runtimeState);

            var input = new TriggeredFunctionData
            {
                TriggerValue = context,
#pragma warning disable CS0618 // Type or member is obsolete (not intended for general public use)
                InvokeHandler = async functionInvoker =>
                {
                    // Invoke the function and look for a return value. Trigger return values are an undocumented feature that we depend on.
                    Task invokeTask = functionInvoker();
                    if (invokeTask is not Task<object> invokeTaskWithResult)
                    {
                        // This should never happen
                        throw new InvalidOperationException("The internal function invoker returned a task that does not support return values!");
                    }

                    // The return value is expected to be a JSON string of a well-known schema.
                    string? triggerReturnValue = (await invokeTaskWithResult) as string;

                    // This will fail with an exception if the language worker gave us some bad/unexpected data. We throw an
                    // exception instead of failing the orchestration since it's not an app issue. Users will likely need to
                    // update their SDK version to mitigate this kind of problem.
                    context.SetResult(triggerReturnValue);
#pragma warning restore CS0618 // Type or member is obsolete (not intended for general public use)
                },
            };

            FunctionResult functionResult;
            try
            {
                functionResult = await function.Executor.TryExecuteAsync(
                    input,
                    cancellationToken: this.HostLifetimeService.OnStopping);
            }
            catch (Exception hostRuntimeException)
            {
                string reason = this.HostLifetimeService.OnStopping.IsCancellationRequested ?
                    "The Functions/WebJobs runtime is shutting down!" :
                    $"Unhandled exception in the Functions/WebJobs runtime: {hostRuntimeException}";

                this.TraceHelper.FunctionAborted(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    reason,
                    functionType: FunctionType.Orchestrator);

                // This will abort the current execution and force an durable retry
                throw new SessionAbortedException(reason);
            }

            OrchestratorExecutionResult orchestratorResult;
            if (functionResult.Succeeded)
            {
                orchestratorResult = context.GetResult();

                if (context.OrchestratorCompleted)
                {
                    this.TraceHelper.FunctionCompleted(
                        this.Options.HubName,
                        functionName.Name,
                        instance.InstanceId,
                        this.extension.GetIntputOutputTrace(context.SerializedOutput),
                        context.ContinuedAsNew,
                        FunctionType.Orchestrator,
                        isReplay: false);
#if !FUNCTIONS_V1
                    DurableTaskExtension.TagActivityWithOrchestrationStatus(
                        OrchestrationRuntimeStatus.Completed,
                        instance.InstanceId);
#endif
                    await this.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                        this.Options.HubName,
                        functionName.Name,
                        instance.InstanceId,
                        context.ContinuedAsNew,
                        isReplay: false);
                }
                else
                {
                    this.TraceHelper.FunctionAwaited(
                        this.Options.HubName,
                        functionName.Name,
                        FunctionType.Orchestrator,
                        instance.InstanceId,
                        isReplay: false);
                }
            }
            else
            {
                string exceptionDetails = functionResult.Exception.ToString();

                this.TraceHelper.FunctionFailed(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    exceptionDetails,
                    FunctionType.Orchestrator,
                    isReplay: false);

                await this.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    exceptionDetails,
                    isReplay: false);

                orchestratorResult = OrchestratorExecutionResult.ForFailure(
                    message: $"Function '{functionName}' failed with an unhandled exception.",
                    functionResult.Exception);
            }

            // Send the result of the orchestrator function to the DTFx dispatch pipeline.
            // This allows us to bypass the default, in-process execution and process the given results immediately.
            dispatchContext.SetProperty(orchestratorResult);
        }

        /// <summary>
        /// Durable Task Framework activity middleware that invokes an out-of-process orchestrator function.
        /// </summary>
        /// <param name="dispatchContext">This middleware context provided by the framework that contains information about the activity.</param>
        /// <param name="next">The next middleware handler in the pipeline.</param>
        /// <exception cref="SessionAbortedException">Thrown if there is a recoverable error in the Functions runtime that's expected to be handled gracefully.</exception>
        public async Task CallActivityAsync(DispatchMiddlewareContext dispatchContext, Func<Task> next)
        {
            TaskScheduledEvent? scheduledEvent = dispatchContext.GetProperty<TaskScheduledEvent>();
            if (scheduledEvent == null)
            {
                // This should never happen, and there's no good response we can return if it does.
                throw new InvalidOperationException($"An activity was scheduled but no {nameof(TaskScheduledEvent)} was found!");
            }

            FunctionName functionName = new FunctionName(scheduledEvent.Name);

            OrchestrationInstance? instance = dispatchContext.GetProperty<OrchestrationInstance>();
            if (instance == null)
            {
                // This should never happen, but it's almost certainly non-retriable if it does.
                dispatchContext.SetProperty(new ActivityExecutionResult
                {
                    ResponseEvent = new TaskFailedEvent(
                        eventId: -1,
                        taskScheduledId: scheduledEvent.EventId,
                        reason: $"Function {functionName} could not execute because instance ID metadata was missing!",
                        details: null),
                });
                return;
            }

            if (!this.extension.TryGetActivityInfo(functionName, out RegisteredFunctionInfo? function))
            {
                // Fail the activity call with an error explaining that the function name is invalid.
                string errorMessage = this.extension.GetInvalidActivityFunctionMessage(functionName.Name);
                dispatchContext.SetProperty(new ActivityExecutionResult
                {
                    ResponseEvent = new TaskFailedEvent(
                        eventId: -1,
                        taskScheduledId: scheduledEvent.EventId,
                        reason: errorMessage,
                        details: null),
                });
                return;
            }

            string? rawInput = scheduledEvent.Input;
            this.TraceHelper.FunctionStarting(
                this.Options.HubName,
                functionName.Name,
                instance.InstanceId,
                this.extension.GetIntputOutputTrace(rawInput),
                functionType: FunctionType.Activity,
                isReplay: false,
                taskEventId: scheduledEvent.EventId);

            var inputContext = new DurableActivityContext(this.extension, instance.InstanceId, rawInput, functionName.Name);
            var triggerInput = new TriggeredFunctionData { TriggerValue = inputContext };

            FunctionResult result;
            try
            {
                result = await function.Executor.TryExecuteAsync(
                    triggerInput,
                    cancellationToken: this.HostLifetimeService.OnStopping);
            }
            catch (Exception hostRuntimeException)
            {
                string reason = this.HostLifetimeService.OnStopping.IsCancellationRequested ?
                    "The Functions/WebJobs runtime is shutting down!" :
                    $"Unhandled exception in the Functions/WebJobs runtime: {hostRuntimeException}";

                this.TraceHelper.FunctionAborted(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    reason,
                    functionType: FunctionType.Activity);

                // This will abort the current execution and force an durable retry
                throw new SessionAbortedException(reason);
            }

            ActivityExecutionResult activityResult;
            if (result.Succeeded)
            {
                string? serializedOutput = inputContext.GetSerializedOutput();
                this.TraceHelper.FunctionCompleted(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    this.extension.GetIntputOutputTrace(serializedOutput),
                    continuedAsNew: false,
                    FunctionType.Activity,
                    isReplay: false,
                    scheduledEvent.EventId);

                activityResult = new ActivityExecutionResult
                {
                    ResponseEvent = new TaskCompletedEvent(
                        eventId: -1,
                        taskScheduledId: scheduledEvent.EventId,
                        result: serializedOutput),
                };
            }
            else
            {
                // TODO: Deserialize the out-of-proc exception
                string exceptionDetails = result.Exception.ToString();

                this.TraceHelper.FunctionFailed(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    exceptionDetails,
                    FunctionType.Activity,
                    isReplay: false,
                    scheduledEvent.EventId);

                activityResult = new ActivityExecutionResult
                {
                    ResponseEvent = new TaskFailedEvent(
                        eventId: -1,
                        taskScheduledId: scheduledEvent.EventId,
                        reason: $"Function {functionName} failed with an unhandled exception.",
                        exceptionDetails),
                };
            }

            // Send the result of the activity function to the DTFx dispatch pipeline.
            // This allows us to bypass the default, in-process execution and process the given results immediately.
            dispatchContext.SetProperty(activityResult);
        }
    }
}
