// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class TaskNonexistentActivityShim : TaskActivity
    {
        private readonly DurableTaskExtension config;
        private readonly string activityName;

        public TaskNonexistentActivityShim(
            DurableTaskExtension config,
            string activityName)
        {
            this.config = config;
            this.activityName = activityName;
        }

        public override string Run(TaskContext context, string input)
        {
            string message = $"Activity function '{this.activityName}' does not exist.";
            Exception exceptionToReport = new FunctionFailedException(message);

            throw new TaskFailureException(
                $"Activity function '{this.activityName}' failed: {exceptionToReport.Message}",
                Utils.SerializeCause(exceptionToReport, this.config.ErrorDataConverter));
        }
    }
}
