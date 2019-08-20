// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host.Executors;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Common functionality of <see cref="TaskEntityShim"/> and <see cref="TaskOrchestrationShim"/>.
    /// </summary>
    internal abstract class TaskCommonShim : TaskOrchestration
    {
        public TaskCommonShim(DurableTaskExtensionBase config)
        {
            this.Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public abstract DurableCommonContext Context { get; }

        internal DurableTaskExtensionBase Config { get; private set; }

        protected Func<Task> FunctionInvocationCallback { get; private set; }

        public void SetFunctionInvocationCallback(Func<Task> callback)
        {
            this.FunctionInvocationCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public abstract RegisteredFunctionInfo GetFunctionInfo();
    }
}