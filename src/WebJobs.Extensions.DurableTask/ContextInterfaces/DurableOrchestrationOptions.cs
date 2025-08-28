// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Options for starting a durable orchestration.
    /// </summary>
    public sealed class DurableOrchestrationOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableOrchestrationOptions"/> class.
        /// </summary>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        public DurableOrchestrationOptions(string orchestratorFunctionName)
        {
            this.OrchestratorFunctionName = orchestratorFunctionName;
        }

        /// <summary>
        /// JSON-serializeable input value for the orchestrator function.
        /// </summary>
        public object Input { get; init; }

        /// <summary>
        /// Gets or sets the ID for the new orchestration.
        /// </summary>
        public string InstanceId { get; init; }

        /// <summary>
        /// Gets the name of the orchestrator function to start.
        /// </summary>
        public string OrchestratorFunctionName { get; }

        /// <summary>
        /// Gets or sets tags to associate with the new orchestration.
        /// </summary>
        public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
    }
}
