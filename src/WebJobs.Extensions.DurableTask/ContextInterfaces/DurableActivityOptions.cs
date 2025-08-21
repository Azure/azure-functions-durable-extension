using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    public sealed class DurableActivityOptions
    {
        public DurableActivityOptions(string functionName)
        {
            this.FunctionName = functionName;
        }

        public string FunctionName { get; }

        public object Input { get; init; }

        public RetryOptions RetryOptions { get; init; }

        public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
    }
}