// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
#if FUNCTIONS_V3_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

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
                DurableTaskExtension.TagActivityWithOrchestrationStatus(OrchestrationRuntimeStatus.Running, instance.InstanceId);
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
                    if (string.IsNullOrEmpty(triggerReturnValue))
                    {
                        throw new InvalidOperationException(
                            "The function invocation resulted in a null response. This means that either the orchestrator function was implemented " +
                            "incorrectly, the Durable Task language SDK was implemented incorrectly, or that the destination language worker is not " +
                            "sending the function result back to the host.");
                    }

                    byte[] triggerReturnValueBytes = Convert.FromBase64String(triggerReturnValue);
                    var response = Microsoft.DurableTask.Protobuf.OrchestratorResponse.Parser.ParseFrom(triggerReturnValueBytes);
                    context.SetResult(
                        response.Actions.Select(ProtobufUtils.ToOrchestratorAction),
                        response.CustomStatus);
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

            if (!string.IsNullOrEmpty(scheduledEvent.Name) && scheduledEvent.Name.StartsWith("BuiltIn::HttpActivity"))
            {
                try
                {
                    if (dispatchContext.GetProperty<TaskActivity>() is TaskHttpActivityShim shim)
                    {
                        OrchestrationInstance orchestrationInstance = dispatchContext.GetProperty<OrchestrationInstance>();
                        TaskContext context = new TaskContext(orchestrationInstance);

                        // convert the DurableHttpRequest
                        DurableHttpRequest? req = ConvertDurableHttpRequest(scheduledEvent.Input);
                        IList<DurableHttpRequest> list = new List<DurableHttpRequest>() { req };
                        string serializedRequest = JsonConvert.SerializeObject(list);

                        string? output = await shim.RunAsync(context, serializedRequest);
                        dispatchContext.SetProperty(new ActivityExecutionResult
                        {
                            ResponseEvent = new TaskCompletedEvent(
                            eventId: -1,
                            taskScheduledId: scheduledEvent.EventId,
                            result: output),
                        });
                        return;
                    }
                }
                catch (Exception e)
                {
                    dispatchContext.SetProperty(new ActivityExecutionResult
                    {
                        ResponseEvent = new TaskFailedEvent(
                        eventId: -1,
                        taskScheduledId: scheduledEvent.EventId,
                        reason: $"Function failed",
                        details: e.Message),
                    });
                    return;
                }
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
                this.TraceHelper.FunctionFailed(
                    this.Options.HubName,
                    functionName.Name,
                    instance.InstanceId,
                    result.Exception.ToString(),
                    FunctionType.Activity,
                    isReplay: false,
                    scheduledEvent.EventId);

                activityResult = new ActivityExecutionResult
                {
                    ResponseEvent = new TaskFailedEvent(
                        eventId: -1,
                        taskScheduledId: scheduledEvent.EventId,
                        reason: $"Function '{functionName}' failed with an unhandled exception.",
                        details: null,
                        GetFailureDetails(result.Exception)),
                };
            }

            // Send the result of the activity function to the DTFx dispatch pipeline.
            // This allows us to bypass the default, in-process execution and process the given results immediately.
            dispatchContext.SetProperty(activityResult);
        }

        private static DurableHttpRequest? ConvertDurableHttpRequest(string? inputString)
        {
            IList<dynamic>? input = JsonConvert.DeserializeObject<IList<dynamic>>(inputString);
            dynamic? dynamicRequest = input[0];

            HttpMethod httpMethod = dynamicRequest.method.ToObject<HttpMethod>();
            Uri uri = dynamicRequest.uri.ToObject<Uri>();
            string content = dynamicRequest.content.ToString();

            JsonSerializerSettings settings = new JsonSerializerSettings { Converters = new List<JsonConverter> { new HttpHeadersConverter() } };
            Dictionary<string, StringValues> headers = JsonConvert.DeserializeObject<Dictionary<string, StringValues>>(dynamicRequest.headers.ToString(), settings);

            DurableHttpRequest request = new DurableHttpRequest(httpMethod, uri, headers, content);

            return request;
        }

        private static FailureDetails GetFailureDetails(Exception e)
        {
            if (e.InnerException != null && e.InnerException.Message.StartsWith("Result:"))
            {
                Exception rpcException = e.InnerException;
                if (TryGetRpcExceptionFields(rpcException.Message, out string? exception, out string? stackTrace))
                {
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

        private static bool TrySplitExceptionTypeFromMessage(
            string exception,
            [NotNullWhen(true)] out string? exceptionType,
            [NotNullWhen(true)] out string? exceptionMessage)
        {
            // Example exception messages:
            // .NET   : System.ApplicationException: Kah-BOOOOM!!
            // Java   : SQLServerException: Invalid column name 'status'.
            // Python : ResourceNotFoundError: The specified blob does not exist. RequestId:8d5a2c9b-b01e-006f-33df-3f7a2e000000 Time:2022-03-25T00:31:24.2003327Z ErrorCode:BlobNotFound Error:None
            // Node   : SyntaxError: Unexpected token N in JSON at position 12768

            // From the above examples, they all follow the same pattern of {ExceptionType}: {Message}
            string delimeter = ": ";
            int endExceptionType = exception.IndexOf(delimeter);
            if (endExceptionType < 0)
            {
                exceptionType = null;
                exceptionMessage = null;
                return false;
            }

            exceptionType = exception[..endExceptionType];

            // The .NET Isolated language worker strangely includes the stack trace in the exception message.
            // To avoid bloating the payload with redundant information, we only consider the first line.
            int startMessage = endExceptionType + delimeter.Length;
            int endMessage = exception.IndexOf('\n', startMessage);
            if (endMessage < 0)
            {
                exceptionMessage = exception[startMessage..];
            }
            else
            {
                exceptionMessage = exception[startMessage..endMessage].TrimEnd();
            }

            return true;
        }
    }
}
#endif