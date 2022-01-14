// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Common functionality of <see cref="TaskEntityShim"/> and <see cref="TaskOrchestrationShim"/>.
    /// </summary>
    internal abstract class TaskCommonShim : TaskOrchestration
    {
        private readonly TaskCompletionSource<Exception> timeoutTaskCompletionSource = new TaskCompletionSource<Exception>();

        public TaskCommonShim(DurableTaskExtension config)
        {
            this.Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public abstract DurableCommonContext Context { get; }

        internal DurableTaskExtension Config { get; private set; }

        protected Func<Task> FunctionInvocationCallback { get; private set; }

        internal Task<Exception> TimeoutTask => this.timeoutTaskCompletionSource.Task;

        internal void TimeoutTriggered(Exception exception)
        {
            this.timeoutTaskCompletionSource.TrySetResult(exception);
        }

        public void SetFunctionInvocationCallback(Func<Task> callback)
        {
            this.FunctionInvocationCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public abstract RegisteredFunctionInfo GetFunctionInfo();
    }
}