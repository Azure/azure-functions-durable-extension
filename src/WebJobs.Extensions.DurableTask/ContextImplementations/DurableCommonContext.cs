// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.Core.History;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Common functionality used by both <see cref="DurableOrchestrationContext"/>
    /// and <see cref="DurableEntityContext"/>.
    /// </summary>
    internal abstract class DurableCommonContext
    {
        private readonly List<Func<Task>> deferredTasks
            = new List<Func<Task>>();

        internal DurableCommonContext(DurableTaskExtension config, string functionName)
        {
            this.Config = config ?? throw new ArgumentNullException(nameof(config));
            this.FunctionName = functionName;
        }

        internal DurableTaskExtension Config { get; }

        internal string FunctionName { get; }

        internal string InstanceId { get; set; }

        internal string ExecutionId { get; set; }

        internal IList<HistoryEvent> History { get; set; }

        internal string RawInput { get; set; }

        internal string HubName => this.Config.Options.HubName;

        internal string Name => this.FunctionName;

        internal bool ExecutorCalledBack { get; set; }

        internal void AddDeferredTask(Func<Task> function)
        {
            this.deferredTasks.Add(function);
        }

        internal async Task RunDeferredTasks()
        {
            await Task.WhenAll(this.deferredTasks.Select(x => x()));
            this.deferredTasks.Clear();
        }
    }
}