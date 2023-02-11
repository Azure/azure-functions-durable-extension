// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DurableTask.Core.Entities;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Implements the durable entity.
    /// </summary>
    internal class TaskEntityShim : TaskEntity
    {
        private readonly DurableEntityContext context;
        private readonly DurableTaskExtension config;
        private readonly IApplicationLifetimeWrapper hostServiceLifetime;
        private readonly RegisteredFunctionInfo functionInfo;
        private readonly MessagePayloadDataConverter messageDataConverter;
        private readonly MessagePayloadDataConverter errorDataConverter;
        private readonly TaskCompletionSource<Exception> timeoutTaskCompletionSource = new TaskCompletionSource<Exception>();

        private OperationBatchRequest batchRequest;
        private OperationBatchResult batchResult;
        private Func<Task> functionInvocationCallback;

        public TaskEntityShim(
            DurableTaskExtension config,
            IApplicationLifetimeWrapper hostServiceLifetime,
            string schedulerId)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.hostServiceLifetime = hostServiceLifetime;
            this.messageDataConverter = config.MessageDataConverter;
            this.errorDataConverter = config.ErrorDataConverter;
            this.InstanceId = schedulerId;
            this.EntityId = EntityId.GetEntityIdFromSchedulerId(schedulerId);
            this.context = new DurableEntityContext(config, this.EntityId, this);

            FunctionName entityFunction = new FunctionName(this.context.FunctionName);
            this.functionInfo = this.config.GetEntityInfo(entityFunction);
        }

        public string InstanceId { get; }

        public EntityId EntityId { get; }

        public bool RollbackFailedOperations => this.config.Options.RollbackEntityOperationsOnExceptions;

        internal OperationBatchRequest BatchRequest => this.batchRequest;

        internal OperationBatchResult BatchResult => this.batchResult;

        internal string HubName => this.config.Options.HubName;

        internal string Name => this.context.FunctionName;

        internal Task<Exception> TimeoutTask => this.timeoutTaskCompletionSource.Task;

        internal void TimeoutTriggered(Exception exception)
        {
            this.timeoutTaskCompletionSource.TrySetResult(exception);
        }

        public override async Task<OperationBatchResult> ExecuteOperationBatchAsync(OperationBatchRequest batchRequest)
        {
            this.batchRequest = batchRequest;
            this.batchResult = new OperationBatchResult()
            {
                Results = new List<OperationResult>(),
                Actions = new List<OperationAction>(),
                EntityState = batchRequest.EntityState,
            };

            // Start the functions invocation pipeline (billing, logging, bindings, and timeout tracking).
            WrappedFunctionResult result = await FunctionExecutionHelper.ExecuteFunctionInEntityMiddleware(
            this.functionInfo.Executor,
            new TriggeredFunctionData
            {
                TriggerValue = this.context,
#pragma warning disable CS0618 // Approved for use by this extension
                InvokeHandler = async userCodeInvoker =>
                {
                    this.context.ExecutorCalledBack = true;

                    this.functionInvocationCallback = userCodeInvoker;

                    this.config.TraceHelper.FunctionStarting(
                        this.HubName,
                        this.Name,
                        this.InstanceId,
                        this.config.GetIntputOutputTrace(this.batchRequest.EntityState),
                        FunctionType.Entity,
                        isReplay: false);

                    // Run all the operations in the batch
                    if (this.context.InternalError == null)
                    {
                        try
                        {
                            var stopwatch = new Stopwatch();
                            stopwatch.Start();

                            // call the function once per operation in the batch
                            for (int i = 0; i < this.batchRequest.Operations.Count; i++)
                            {
                                // first, check if we should stop here and not execute the rest of the batch
                                if (this.hostServiceLifetime.OnStopping.IsCancellationRequested // host is shutting down
                                    || this.TimeoutTask.IsCompleted // we have timed out
                                    || stopwatch.Elapsed > TimeSpan.FromMinutes(1)) // we have spent significant time in this batch
                                {
                                    break;
                                }

                                // execute the operation
                                await this.ProcessOperationRequestAsync(i);
                            }

                            // Commit the effects of this batch, if
                            // we have not already run into an internal error
                            // (in which case we will abort the batch instead of committing it)
                            if (this.context.InternalError == null)
                            {
                                bool writeBackSuccessful = true;
                                OperationResult serializationErrorMessage = null;

                                if (this.RollbackFailedOperations)
                                {
                                    // the state has already been written back, since it is
                                    // done right after each operation.
                                }
                                else
                                {
                                    // we are writing back the state only now, after the whole batch is complete
                                    writeBackSuccessful = this.context.TryWriteback(out serializationErrorMessage, out var _);

                                    if (!writeBackSuccessful) // now all operations are considered failed and rolled back
                                    {
                                        // we clear the actions
                                        this.batchResult.Actions.Clear();

                                        // we replace all non-error response messages with the serialization error message
                                        for (int i = 0; i < this.batchResult.Results.Count; i++)
                                        {
                                            this.batchResult.Results[i] = serializationErrorMessage;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            this.context.CaptureInternalError(e);
                        }
                    }

                    // 5. If there were internal or application errors, trace them for DF
                    if (this.context.ErrorsPresent(out var description))
                    {
                        this.config.TraceHelper.FunctionFailed(
                            this.HubName,
                            this.Name,
                            this.InstanceId,
                            description,
                            functionType: FunctionType.Entity,
                            isReplay: false);
                    }
                    else
                    {
                        this.config.TraceHelper.FunctionCompleted(
                            this.HubName,
                            this.Name,
                            this.InstanceId,
                            this.config.GetIntputOutputTrace(this.context.LastSerializedState ?? ""),
                            continuedAsNew: true,
                            functionType: FunctionType.Entity,
                            isReplay: false);
                    }

                    // 6. If there were internal or application errors, also rethrow them here so the functions host gets to see them
                    this.context.ThrowInternalExceptionIfAny();
                    this.context.ThrowApplicationExceptionsIfAny();
                },
#pragma warning restore CS0618
            },
            this,
            this.context,
            this.hostServiceLifetime.OnStopping);

            if (result.ExecutionStatus == WrappedFunctionResult.FunctionResultStatus.FunctionTimeoutError)
            {
                await this.TimeoutTask;
            }

            if (result.ExecutionStatus == WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError
                || result.ExecutionStatus == WrappedFunctionResult.FunctionResultStatus.FunctionsHostStoppingError)
            {
                this.config.TraceHelper.FunctionAborted(
                   this.HubName,
                   this.Name,
                   this.InstanceId,
                   $"An internal error occurred while attempting to execute this function. The execution will be aborted and retried. Details: {result.Exception}",
                   functionType: FunctionType.Entity);

                // This will abort the execution and cause the message to go back onto the queue for re-processing
                throw new SessionAbortedException(
                    $"An internal error occurred while attempting to execute '{this.Name}'.",
                    result.Exception);
            }

            return this.batchResult;
        }

        public RegisteredFunctionInfo GetFunctionInfo()
        {
            FunctionName entityFunction = new FunctionName(this.context.FunctionName);
            return this.config.GetEntityInfo(entityFunction);
        }

        private async Task ProcessOperationRequestAsync(int index)
        {
            // set context for operation
            var operation = this.batchRequest.Operations[index];
            this.context.CurrentOperationIndex = index;
            this.context.CurrentOperation = operation;
            this.context.CurrentOperationResult = new OperationResult();

            // set the async-local static context that is visible to the application code
            Entity.SetContext(this.context);

            bool operationFailed = false;
            var positionBeforeCurrentOperation = this.batchResult.Actions.Count;

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            Exception exception = null;

            try
            {
                Task invokeTask = this.functionInvocationCallback();
                if (invokeTask is Task resultTask)
                {
                    var completedTask = await Task.WhenAny(resultTask, this.TimeoutTask);

                    if (completedTask == this.TimeoutTask)
                    {
                        exception = await this.TimeoutTask;
                    }
                    else
                    {
                        await resultTask;
                    }
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that is not awaitable!");
                }
            }
            catch (Exception e)
            {
                exception = e;
            }

            stopwatch.Stop();

            if (exception != null)
            {
                this.context.CaptureApplicationError(exception);

                // exception must be sent with response back to caller
                this.context.CurrentOperationResult.SetExceptionResult(
                    exception,
                    this.context.CurrentOperation.Operation,
                    this.errorDataConverter);

                operationFailed = true;
            }

            if (this.RollbackFailedOperations)
            {
                // we write back the entity state after each successful operation
                if (!operationFailed)
                {
                    if (!this.context.TryWriteback(out var errorResult, out exception, operation.Operation, operation.Id.ToString()))
                    {
                        // state serialization failed; create error response and roll back.
                        this.context.CurrentOperationResult = errorResult;
                        operationFailed = true;
                    }
                }

                if (operationFailed)
                {
                    // discard changes and don't send any signals
                    this.context.Rollback(positionBeforeCurrentOperation);
                }
            }

            // clear the async-local static context that is visible to the application code
            Entity.SetContext(null);

            // read the result and clear the context
            var result = this.context.CurrentOperationResult;
            this.context.CurrentOperation = null;
            this.context.CurrentOperationIndex = -1;
            this.context.CurrentOperationResult = null;

            if (!operationFailed)
            {
                this.config.TraceHelper.OperationCompleted(
                        this.HubName,
                        this.Name,
                        this.InstanceId,
                        operation.Id.ToString(),
                        operation.Operation,
                        this.config.GetIntputOutputTrace(operation.Input),
                        this.config.GetIntputOutputTrace(result.Result),
                        stopwatch.Elapsed.TotalMilliseconds,
                        isReplay: false);
            }
            else
            {
                this.config.TraceHelper.OperationFailed(
                        this.HubName,
                        this.Name,
                        this.InstanceId,
                        operation.Id.ToString(),
                        operation.Operation,
                        this.config.GetIntputOutputTrace(operation.Input),
                        exception.ToString(),
                        stopwatch.Elapsed.TotalMilliseconds,
                        isReplay: false);
            }

            // write the result to the list of results for the batch
            this.batchResult.Results.Add(result);
        }

        // DRAFT deleted out-of-proc section, need to bring it back
    }
}
