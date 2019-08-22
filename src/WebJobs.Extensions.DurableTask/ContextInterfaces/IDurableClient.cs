using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextInterfaces
{
    /// <summary>
    /// Provides functionality available to durable orchestration and entity clients.
    /// </summary>
    public interface IDurableClient : IDurableOrchestrationClient, IDurableEntityClient
    {
        /// <summary>
        /// Gets the name of the task hub configured on this client instance.
        /// </summary>
        /// <value>
        /// The name of the task hub.
        /// </value>
        string TaskHubName { get; }
    }
}
