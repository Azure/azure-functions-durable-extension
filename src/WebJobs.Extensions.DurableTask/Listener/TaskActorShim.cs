using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host.Executors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Implements the actor scheduler as a looping orchestration.
    /// There is one such orchestration per actor.
    /// The orchestration terminates if the actor is deleted and idle.
    /// The orchestration calls ContinueAsNew when it is idle, but not deleted.
    /// </summary>
    internal class TaskActorShim : TaskCommonShim
    {
        private readonly DurableActorContext context;

        private TaskCompletionSource<object> shutDownSignal
            = new TaskCompletionSource<object>();

        private Task lastStartedOperation = Task.CompletedTask;

        private Dictionary<Guid, Task> currentReentrantRequests = new Dictionary<Guid, Task>();

        private int remainingReceiveLimit = 20; // TODO make configurable and/or compute based on state size

        public TaskActorShim(DurableTaskExtension config, string schedulerId)
            : base(config)
        {
            this.SchedulerId = schedulerId;
            this.ActorId = GetActorIdFromSchedulerId(schedulerId);
            this.context = new DurableActorContext(config, this.ActorId);
        }

        public override DurableCommonContext Context => context;

        public string SchedulerId { get; private set; }

        public ActorId ActorId { get; private set; }

        public static string GetSchedulerIdFromActorId(ActorId actor)
        {
            return $"@{@actor.ActorClass}@{actor.ActorKey}";
        }

        public static ActorId GetActorIdFromSchedulerId(string schedulerId)
        {
            var pos = schedulerId.IndexOf('@', 1);
            var actorClass = schedulerId.Substring(1, pos - 1);
            var actorKey = schedulerId.Substring(pos + 1);
            return new ActorId(actorClass, actorKey);
        }

        public override RegisteredFunctionInfo GetFunctionInfo()
        {
            FunctionName actorFunction = new FunctionName(this.Context.FunctionName);
            return this.Config.GetActorInfo(actorFunction);
        }

        public override string GetStatus()
        {
            if (this.lastStartedOperation.IsCompleted)
            {
                return "Available";
            }
            else
            {
                if (this.context.State.Queue != null)
                {
                    return $"Busy ({this.context.State.Queue.Count} Queued)";
                }
                else
                {
                    return "Busy";
                }
            }
        }

        private void SignalShutdown()
        {
            MethodInfo trySetResult = typeof(TaskCompletionSource<object>).GetMethod("TrySetResult");
            trySetResult.Invoke(this.shutDownSignal, new[] { (object)null });
            this.shutDownSignal.TrySetResult(0);
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            this.Config.TraceHelper.ExternalEventRaised(
                this.Context.HubName,
                this.Context.Name,
                this.Context.InstanceId,
                eventName,
                this.Config.GetIntputOutputTrace(serializedEventData),
                this.Context.IsReplaying);

            if (eventName == "op")
            {
                var operationMessage = JsonConvert.DeserializeObject<OperationMessage>(serializedEventData);

                if (this.lastStartedOperation.IsCompleted && this.remainingReceiveLimit > 0)
                {
                    this.lastStartedOperation = this.ProcessRequest(operationMessage);

                    this.remainingReceiveLimit--;
                }
                else
                {
                    this.context.State.Enqueue(operationMessage);

                    if (this.remainingReceiveLimit == 0)
                    {
                        this.SignalShutdown();
                    }
                }
            }
            else // it's a response
            {
                this.Context.RaiseEvent(eventName, serializedEventData);
            }
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            if (this.FunctionInvocationCallback == null)
            {
                throw new InvalidOperationException($"The {nameof(this.FunctionInvocationCallback)} has not been assigned!");
            }

            if (this.GetFunctionInfo().IsOutOfProc)
            {
                throw new NotImplementedException("out-of-proc actor support is not implemented yet");
            }

            if (!innerContext.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.IsReplaying));
            }

            try
            {
                this.context.InnerContext = innerContext;

                if (serializedInput == null)
                {
                    // this instance was automatically started by DTFx
                    this.context.State = new SchedulerState();
                }
                else
                {
                    // a previous incarnation of this instance called continueAsNew
                    this.context.State = JsonConvert.DeserializeObject<SchedulerState>(serializedInput, MessagePayloadDataConverter.MessageSettings);
                }

                // if there were messages in the queue left over, start processing them
                while (this.lastStartedOperation.IsCompleted && this.context.State.TryDequeue(out var request))
                {
                    this.lastStartedOperation = this.ProcessRequest(request);
                }

                await this.shutDownSignal.Task;

                await this.lastStartedOperation;

                // TODO see if we can DTFx to restart orchestrations that are done
                // in the meantime simply let's never be done
                // bool continueAsNew = !this.context.State.IsEmpty;
                bool continueAsNew = true;

                if (continueAsNew)
                {
                    this.context.State.CurrentStateView?.WriteBack();
                    var jstate = JToken.FromObject(this.context.State);
                    this.context.InnerContext.ContinueAsNew(jstate);
                }

                this.Config.TraceHelper.FunctionCompleted(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.State.ToString(),
                    continueAsNew,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying);

                if (!this.context.IsReplaying)
                {
                    this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        continueAsNew,
                        this.context.IsReplaying));
                }

                return continueAsNew ? "continue" : "done";
            }
            catch (Exception e)
            {
                // something went wrong in our code (application exceptions don't make it here)

                string exceptionDetails = e.ToString();

                if (!this.context.IsReplaying)
                {
                    this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        exceptionDetails,
                        this.context.IsReplaying));
                }

                var orchestrationException = new OrchestrationFailureException(
                    $"Actor scheduler for '{this.context.Name}' failed: {e.Message}",
                    Utils.SerializeCause(e, MessagePayloadDataConverter.ErrorConverter));

                this.context.OrchestrationException = ExceptionDispatchInfo.Capture(orchestrationException);

                throw orchestrationException;
            }
            finally
            {
                this.context.IsCompleted = true;
            }
        }

        private async Task ProcessRequest(OperationMessage request)
        {
            // set context for operation
            this.context.CurrentOperation = request;
            this.context.CurrentOperationResponse = new ResponseMessage();
            this.context.Fresh = !this.context.State.ActorExists;
            this.context.State.ActorExists = true;
            this.context.DestructOnExit = false;

            this.Config.TraceHelper.FunctionStarting(
                                    this.context.HubName,
                                    this.context.Name,
                                    this.context.InstanceId,
                                    this.Config.GetIntputOutputTrace(request.Argument),
                                    FunctionType.Actor,
                                    this.context.IsReplaying);
            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task resultTask)
                {
                    await resultTask;
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that is not awaitable!");
                }
            }
            catch (Exception e)
            {
                // exception must be sent with response back to caller
                this.context.CurrentOperationResponse.SetExceptionResult(e, this.context.CurrentOperation.Operation, this.ActorId);

                string exceptionDetails = e.ToString();

                this.Config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    exceptionDetails,
                    FunctionType.Actor,
                    this.context.IsReplaying);
            }

            // read and clear context
            var response = this.context.CurrentOperationResponse;
            var destructOnExit = this.context.DestructOnExit;
            this.context.CurrentOperation = null;
            this.context.CurrentOperationResponse = null;

            // send response
            // TODO think about how to handle exceptions in signals
            if (!request.IsSignal)
            {
                var target = new OrchestrationInstance() { InstanceId = request.ParentInstanceId };
                var guid = request.Id.ToString();
                var jresponse = JToken.FromObject(response, MessagePayloadDataConverter.DefaultSerializer);
                this.context.InnerContext.SendEvent(target, guid, jresponse);
            }

            // destruct actor state if application code requested it
            if (destructOnExit)
            {
                this.context.State.ActorExists = false;
                this.context.State.CurrentStateView = null;
                this.context.State.ActorState = null;
            }

            // start next request waiting in the queue if there is one
            if (this.context.State.TryDequeue(out var operationMessage))
            {
                this.lastStartedOperation = this.ProcessRequest(operationMessage);
            }

            // if there are no waiting requests and the actor is deleted, shut down
            else if (destructOnExit)
            {
                this.SignalShutdown();
            }
        }
    }
}
