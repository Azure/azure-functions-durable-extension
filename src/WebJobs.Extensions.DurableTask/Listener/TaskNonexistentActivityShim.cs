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
        private readonly string message;

        public TaskNonexistentActivityShim(
            DurableTaskExtension config,
            string activityName,
            string message)
        {
            this.config = config;
            this.activityName = activityName;
            this.message = message;
        }

        public override string Run(TaskContext context, string input)
        {
            Exception exceptionToReport = new FunctionFailedException(this.message);

            throw new TaskFailureException(
                $"Activity function '{this.activityName}' failed: {exceptionToReport.Message}",
                Utils.SerializeCause(exceptionToReport, this.config.ErrorDataConverter));
        }
    }
}
