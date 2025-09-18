// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Entities;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host.Executors;
using Newtonsoft.Json;
using P = Microsoft.DurableTask.Protobuf;

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
                // This should never happen, but it's almost certainly non-retryable if it does.
                dispatchContext.SetProperty(OrchestratorExecutionResult.ForFailure(
                    message: "Orchestration runtime state was missing!",
                    details: null));
                return;
            }

            OrchestrationInstance? instance = dispatchContext.GetProperty<OrchestrationInstance>();
            if (instance == null)
            {
                // This should never happen, but it's almost certainly non-retryable if it does.
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

            TaskOrchestrationEntityParameters? entityParameters = dispatchContext.GetProperty<TaskOrchestrationEntityParameters>();

            bool isReplaying = runtimeState.PastEvents.Any();

            this.TraceHelper.FunctionStarting(
                this.Options.HubName,
                functionName.Name,
                instance.InstanceId,
                startEvent.Input,
                FunctionType.Orchestrator,
                isReplaying);

            // One-time logging/notifications for when the orchestration first starts.
            if (!isReplaying)
            {
                DurableTaskExtension.TagActivityWithOrchestrationStatus(OrchestrationRuntimeStatus.Running, instance.InstanceId);
                await this.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    isReplay: false);
            }

            WorkItemMetadata workItemMetadata = dispatchContext.GetProperty<WorkItemMetadata>();
            bool isExtendedSession = workItemMetadata.IsExtendedSession;
            bool includePastEvents = workItemMetadata.IncludePastEvents;

            var context = new RemoteOrchestratorContext(runtimeState, entityParameters, this.extension.Options, isExtendedSession, includePastEvents);
            bool workerRequiresHistory = false;

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
                    if (string.IsNullOrEmpty(triggerReturnValue))
                    {
                        throw new InvalidOperationException(
                            "The function invocation resulted in a null response. This means that either the orchestrator function was implemented " +
                            "incorrectly, the Durable Task language SDK was implemented incorrectly, or that the destination language worker is not " +
                            "sending the function result back to the host.");
                    }

                    byte[] triggerReturnValueBytes = Convert.FromBase64String(triggerReturnValue);
                    P.OrchestratorResponse response = P.OrchestratorResponse.Parser.ParseFrom(triggerReturnValueBytes);

                    workerRequiresHistory = response.RequiresHistory;

                    // TrySetResult may throw if a platform-level error is encountered (like an out of memory exception).
                    context.SetResult(
                        response.Actions.Select(ProtobufUtils.ToOrchestratorAction),
                        response.CustomStatus);

                    // Here we throw if the orchestrator completed with an application-level error. When we do this,
                    // the function's result type will be of type `OrchestrationFailureException` which is reserved
                    // for application-level errors that do not need to be re-tried.
                    context.ThrowIfFailed();
                },
#pragma warning restore CS0618 // Type or member is obsolete (not intended for general public use)
            };

            FunctionResult functionResult;
            try
            {
                functionResult = await function.Executor.TryExecuteAsync(
                    input,
                    cancellationToken: this.HostLifetimeService.OnStopping);
                if (!functionResult.Succeeded)
                {
                    // Shutdown can surface as a completed invocation in a failed state.
                    // Re-throw so we can abort this invocation.
                    this.HostLifetimeService.OnStopping.ThrowIfCancellationRequested();
                }

                // we abort the invocation on "platform level errors" such as:
                // - a timeout
                // - an out of memory exception
                // - a worker process exit
                if (functionResult.Exception is Host.FunctionTimeoutException
                    || functionResult.Exception?.InnerException is SessionAbortedException // see RemoteOrchestrationContext.TrySetResultInternal for details on OOM-handling
                    || (functionResult.Exception?.InnerException?.GetType().ToString().Contains("WorkerProcessExitException") ?? false))
                {
                    // TODO: the `WorkerProcessExitException` type is not exposed in our dependencies, it's part of WebJobs.Host.Script.
                    // Should we add that dependency or should it be exposed in WebJobs.Host?
                    throw functionResult.Exception;
                }
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
                if (workerRequiresHistory)
                {
                    throw new SessionAbortedException("The worker has since ended the extended session and needs an orchestration history to execute the orchestration request.");
                }

                orchestratorResult = context.GetResult();

                if (context.OrchestratorCompleted)
                {
                    this.TraceHelper.FunctionCompleted(
                        this.Options.HubName,
                        functionName.Name,
                        instance.InstanceId,
                        context.SerializedOutput,
                        context.ContinuedAsNew,
                        FunctionType.Orchestrator,
                        isReplay: false);

                    DurableTaskExtension.TagActivityWithOrchestrationStatus(
                        OrchestrationRuntimeStatus.Completed,
                        instance.InstanceId);

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
            else if (context.TryGetOrchestrationErrorDetails(out Exception? exception))
            {
                // the function failed because the orchestrator failed.

                orchestratorResult = context.GetResult();

                this.TraceHelper.FunctionFailed(
                     this.Options.HubName,
                     functionName.Name,
                     instance.InstanceId,
                     exception,
                     FunctionType.Orchestrator,
                     isReplay: false);

                await this.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    exception?.Message ?? string.Empty,
                    isReplay: false);
            }
            else
            {
                // the function failed for some other reason
                string exceptionDetails = functionResult.Exception?.ToString() ?? "Framework-internal message: exception details could not be extracted";

                this.TraceHelper.FunctionFailed(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    functionResult.Exception,
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
                    functionResult.Exception ?? new Exception($"Function '{functionName}' failed with an unknown unhandled exception"));
            }

            // Send the result of the orchestrator function to the DTFx dispatch pipeline.
            // This allows us to bypass the default, in-process execution and process the given results immediately.
            dispatchContext.SetProperty(orchestratorResult);
        }

        /// <summary>
        /// Durable Task Framework entity middleware that invokes an out-of-process orchestrator function.
        /// </summary>
        /// <param name="dispatchContext">This middleware context provided by the framework that contains information about the entity.</param>
        /// <param name="next">The next middleware handler in the pipeline.</param>
        /// <exception cref="SessionAbortedException">Thrown if there is a recoverable error in the Functions runtime that's expected to be handled gracefully.</exception>
        public async Task CallEntityAsync(DispatchMiddlewareContext dispatchContext, Func<Task> next)
        {
            EntityBatchRequest? batchRequest = dispatchContext.GetProperty<EntityBatchRequest>();

            if (batchRequest == null)
            {
                // This should never happen, and there's no good response we can return if it does.
                throw new InvalidOperationException($"An entity was scheduled but no {nameof(EntityBatchRequest)} was found!");
            }

            if (batchRequest.InstanceId == null)
            {
                // This should never happen, and there's no good response we can return if it does.
                throw new InvalidOperationException($"An entity was scheduled but InstanceId is null!");
            }

            EntityId entityId = EntityId.GetEntityIdFromSchedulerId(batchRequest.InstanceId);
            FunctionName functionName = new FunctionName(entityId.EntityName);
            RegisteredFunctionInfo functionInfo = this.extension.GetEntityInfo(functionName);

            void SetErrorResult(FailureDetails failureDetails)
            {
                // Returns a result with no operation results and no state change,
                // and with failure details that explain what error was encountered.
                dispatchContext.SetProperty(new EntityBatchResult()
                {
                    Actions = new List<OperationAction>(),
                    Results = new List<OperationResult>(),
                    EntityState = batchRequest!.EntityState,
                    FailureDetails = failureDetails,
                });
            }

            if (functionInfo == null)
            {
                SetErrorResult(new FailureDetails(
                    errorType: "EntityFunctionNotFound",
                    errorMessage: this.extension.GetInvalidEntityFunctionMessage(functionName.Name),
                    stackTrace: null,
                    innerFailure: null,
                    isNonRetriable: true));
                return;
            }

            this.TraceHelper.FunctionStarting(
                this.Options.HubName,
                functionName.Name,
                batchRequest.InstanceId,
                batchRequest.EntityState,
                functionType: FunctionType.Entity,
                isReplay: false);

            var context = new RemoteEntityContext(batchRequest);

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

                    // The return value is expected to be a base64 string containing the protobuf-encoding of the batch result.
                    string? triggerReturnValue = (await invokeTaskWithResult) as string;
                    if (string.IsNullOrEmpty(triggerReturnValue))
                    {
                        throw new InvalidOperationException(
                            "The function invocation resulted in a null response. This means that either the entity function was implemented " +
                            "incorrectly, the Durable Task language SDK was implemented incorrectly, or that the destination language worker is not " +
                            "sending the function result back to the host.");
                    }

                    byte[] triggerReturnValueBytes = Convert.FromBase64String(triggerReturnValue);
                    P.EntityBatchResult response = P.EntityBatchResult.Parser.ParseFrom(triggerReturnValueBytes);
                    context.Result = response.ToEntityBatchResult();

                    context.ThrowIfFailed();
#pragma warning restore CS0618 // Type or member is obsolete (not intended for general public use)
                },
            };

            FunctionResult functionResult;
            try
            {
                functionResult = await functionInfo.Executor.TryExecuteAsync(
                    input,
                    cancellationToken: this.HostLifetimeService.OnStopping);

                if (!functionResult.Succeeded)
                {
                    // Shutdown can surface as a completed invocation in a failed state.
                    // Re-throw so we can abort this invocation.
                    this.HostLifetimeService.OnStopping.ThrowIfCancellationRequested();
                }
            }
            catch (Exception hostRuntimeException)
            {
                string reason = this.HostLifetimeService.OnStopping.IsCancellationRequested ?
                    "The Functions/WebJobs runtime is shutting down!" :
                    $"Unhandled exception in the Functions/WebJobs runtime: {hostRuntimeException}";

                this.TraceHelper.FunctionAborted(
                    this.Options.HubName,
                    functionName.Name,
                    batchRequest.InstanceId,
                    reason,
                    functionType: FunctionType.Entity);

                // This will abort the current execution and force an durable retry
                throw new SessionAbortedException(reason);
            }

            if (!functionResult.Succeeded)
            {
                this.TraceHelper.FunctionFailed(
                    this.Options.HubName,
                    functionName.Name,
                    batchRequest.InstanceId,
                    functionResult.Exception,
                    FunctionType.Entity,
                    isReplay: false);

                if (context.Result != null)
                {
                    // Send the results of the entity batch execution back to the DTFx dispatch pipeline.
                    // This is important so we can propagate the individual failure details of each failed operation back to the
                    // calling orchestrator. Also, even though the function execution was reported as a failure,
                    // it may not be a "total failure", i.e. some of the operations in the batch may have succeeded and updated
                    // the entity state.
                    dispatchContext.SetProperty(context.Result);
                }
                else
                {
                    SetErrorResult(new FailureDetails(
                        errorType: "FunctionInvocationFailed",
                        errorMessage: $"Invocation of function '{functionName}' failed with an exception.",
                        stackTrace: null,
                        innerFailure: new FailureDetails(functionResult.Exception),
                        isNonRetriable: true));
                }

                return;
            }

            EntityBatchResult batchResult = context.Result
                ?? throw new InvalidOperationException($"The entity function executed successfully but {nameof(context.Result)} is still null!");

            this.TraceHelper.FunctionCompleted(
                       this.Options.HubName,
                       functionName.Name,
                       batchRequest.InstanceId,
                       batchRequest.EntityState,
                       batchResult.EntityState != null,
                       FunctionType.Entity,
                       isReplay: false);

            // Send the results of the entity batch execution back to the DTFx dispatch pipeline.
            dispatchContext.SetProperty(batchResult);
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

            if (scheduledEvent.Name?.StartsWith("BuiltIn::", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                await next();
                return;
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
                rawInput,
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
                if (!result.Succeeded)
                {
                    // Shutdown can surface as a completed invocation in a failed state.
                    // Re-throw so we can abort this invocation.
                    this.HostLifetimeService.OnStopping.ThrowIfCancellationRequested();
                }
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
                    serializedOutput,
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
                this.TraceHelper.FunctionFailed(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    result.Exception,
                    FunctionType.Activity,
                    isReplay: false,
                    scheduledEvent.EventId);

                bool detailsParsedFromSerializedException;

                activityResult = new ActivityExecutionResult
                {
                    ResponseEvent = new TaskFailedEvent(
                        eventId: -1,
                        taskScheduledId: scheduledEvent.EventId,
                        reason: $"Function '{functionName}' failed with an unhandled exception.",
                        details: null,
                        GetFailureDetails(result.Exception, out detailsParsedFromSerializedException)),
                };

                if (!detailsParsedFromSerializedException && this.extension.PlatformInformationService.GetWorkerRuntimeType() == WorkerRuntimeType.DotNetIsolated)
                {
                    this.TraceHelper.ExtensionWarningEvent(
                        this.Options.HubName,
                        functionName.Name,
                        instance.InstanceId,
                        "Failure details not parsed from serialized exception details, worker failed to serialize exception");
                }
            }

            // Send the result of the activity function to the DTFx dispatch pipeline.
            // This allows us to bypass the default, in-process execution and process the given results immediately.
            dispatchContext.SetProperty(activityResult);
        }

        private static FailureDetails GetFailureDetails(Exception e, out bool fromSerializedException)
        {
            fromSerializedException = false;
            if (e.InnerException != null && e.InnerException.Message.StartsWith("Result:"))
            {
                Exception rpcException = e.InnerException;
                if (TryGetRpcExceptionFields(rpcException.Message, out string? exception, out string? stackTrace))
                {
                    if (TryExtractSerializedFailureDetailsFromException(exception, out FailureDetails? details) && details is not null)
                    {
                        fromSerializedException = true;
                        return details;
                    }

                    if (TrySplitExceptionTypeFromMessage(exception, out string? exceptionType, out string? exceptionMessage))
                    {
                        return new FailureDetails(exceptionType, exceptionMessage, stackTrace, innerFailure: null, isNonRetriable: false);
                    }

                    return new FailureDetails("(unknown)", exception, stackTrace, innerFailure: null, isNonRetriable: false);
                }
                else
                {
                    // Don't recognize this message format - return the failure as-is
                    return new FailureDetails(rpcException);
                }
            }

            // Don't recognize this exception - return it as-is
            return new FailureDetails(e);
        }

        private static FailureDetails? GetFailureDetails(P.TaskFailureDetails taskFailureDetails)
        {
            if (taskFailureDetails is null)
            {
                return null;
            }

            return new FailureDetails(
                taskFailureDetails.ErrorType ?? string.Empty,
                taskFailureDetails.ErrorMessage ?? string.Empty,
                taskFailureDetails.StackTrace,
                GetFailureDetails(taskFailureDetails.InnerFailure),
                taskFailureDetails.IsNonRetriable);
        }

        private static bool TryGetRpcExceptionFields(
            string rpcExceptionMessage,
            [NotNullWhen(true)] out string? exception,
            out string? stackTrace)
        {
            exception = null;
            stackTrace = null;

            // The message string is formatted as follows:
            // Result: {result}\nException: {message}\nStack: {stack}

            const string ExceptionDelimeter = "\nException: ";
            const string StackDelimeter = "\nStack: ";

            ReadOnlySpan<char> messageSpan = rpcExceptionMessage.AsSpan();
            int startException = messageSpan.IndexOf(ExceptionDelimeter.AsSpan());
            if (startException == -1)
            {
                // Couldn't find the exception payload - give up
                return false;
            }

            int startExceptionPayload = startException + ExceptionDelimeter.Length;
            ReadOnlySpan<char> exceptionPayloadStartSpan = messageSpan[startExceptionPayload..];
            int endException = exceptionPayloadStartSpan.LastIndexOf(StackDelimeter.AsSpan());
            if (endException >= 0)
            {
                exception = new string(exceptionPayloadStartSpan[..endException].ToArray());

                // Start looking for the stack trace payload immediately after the exception payload
                int startStack = endException + StackDelimeter.Length;
                stackTrace = new string(exceptionPayloadStartSpan[startStack..].ToArray());
            }
            else
            {
                // [Not expected] Couldn't find the stack trace for whatever reason. Just take the rest of the payload as the stack trace.
                exception = new string(exceptionPayloadStartSpan.ToArray());
            }

            return true;
        }

        private static bool TryExtractSerializedFailureDetailsFromException(string exception, out FailureDetails? details)
        {
            try
            {
                if (exception[0] != '{')
                {
                    details = null;
                    return false;
                }

                int newlineIndex = exception.IndexOf('\n');
                string serializedMessage = newlineIndex < 0 ? exception : exception.Substring(0, newlineIndex).Trim();
                P.TaskFailureDetails? taskFailureDetails = JsonConvert.DeserializeObject<P.TaskFailureDetails>(serializedMessage);
                if (taskFailureDetails != null)
                {
                    details = GetFailureDetails(taskFailureDetails);
                    return true;
                }
            }
            catch (Exception)
            {
                // Apparently the exception message was not serialized by the worker middleware, this will be logged in CallActivityAsync()
            }

            details = null;
            return false;
        }

        private static bool TrySplitExceptionTypeFromMessage(
            string exception,
            [NotNullWhen(true)] out string? exceptionType,
            [NotNullWhen(true)] out string? exceptionMessage)
        {
            // In certain situations, like when the .NET Isolated worker is configured with
            // WorkerOptions.EnableUserCodeException = true, the exception message we get from the .NET Isolated
            // worker looks like this:
            // "Exception of type 'ExceptionSerialization.Function+UnknownException' was thrown."
            const string startMarker = "Exception of type '";
            const string endMarker = "' was thrown.";
            if (exception.StartsWith(startMarker) && exception.EndsWith(endMarker))
            {
                exceptionType = exception[startMarker.Length..^endMarker.Length];
                exceptionMessage = string.Empty;
                return true;
            }

            // The following are the more common cases that we expect to see, which will be common across a
            // variety of language workers:
            // .NET   : System.ApplicationException: Kah-BOOOOM!!
            // Java   : SQLServerException: Invalid column name 'status'.
            // Python : ResourceNotFoundError: The specified blob does not exist. RequestId:8d5a2c9b-b01e-006f-33df-3f7a2e000000 Time:2022-03-25T00:31:24.2003327Z ErrorCode:BlobNotFound Error:None
            // Node   : SyntaxError: Unexpected token N in JSON at position 12768

            // From the above examples, they all follow the same pattern of {ExceptionType}: {Message}.
            // However, some exception types override the ToString() method and do something custom, in which
            // case the message may not be in the expected format. In such cases we won't be able to distinguish
            // the exception type.
            string delimeter = ": ";
            int endExceptionType = exception.IndexOf(delimeter);
            if (endExceptionType < 0)
            {
                exceptionType = null;
                exceptionMessage = exception;
                return false;
            }

            exceptionType = exception[..endExceptionType].TrimEnd();

            // The .NET Isolated language worker strangely includes the stack trace in the exception message.
            // To avoid bloating the payload with redundant information, we only consider the first line.
            int startMessage = endExceptionType + delimeter.Length;
            int endMessage = exception.IndexOf('\n', startMessage);
            if (endMessage < 0)
            {
                exceptionMessage = exception[startMessage..].TrimEnd();
            }
            else
            {
                exceptionMessage = exception[startMessage..endMessage].TrimEnd();
            }

            return true;
        }
    }
}