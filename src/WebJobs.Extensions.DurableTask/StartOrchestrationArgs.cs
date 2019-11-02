// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Parameters for starting a new instance of an orchestration.
    /// </summary>
    /// <remarks>
    /// This class is primarily intended for use with <c>IAsyncCollector&lt;T&gt;</c>.
    /// </remarks>
    public class StartOrchestrationArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StartOrchestrationArgs"/> class.
        /// </summary>
        /// <param name="functionName">The name of the orchestrator function to start.</param>
        /// <param name="input">The JSON-serializeable input for the orchestrator function.</param>
        public StartOrchestrationArgs(string functionName, object input)
        {
            this.FunctionName = functionName;
            this.Input = input;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StartOrchestrationArgs"/> class.
        /// </summary>
        public StartOrchestrationArgs()
        { }

        /// <summary>
        /// Gets or sets the name of the orchestrator function to start.
        /// </summary>
        /// <value>The name of the orchestrator function to start.</value>
        public string FunctionName { get; set; }

        /// <summary>
        /// Gets or sets the instance ID to assign to the started orchestration.
        /// </summary>
        /// <remarks>
        /// If this property value is null (the default), then a randomly generated instance ID will be assigned automatically.
        /// </remarks>
        /// <value>The instance ID to assign.</value>
        public string InstanceId { get; set; }

        /// <summary>
        /// Gets or sets the JSON-serializeable input data for the orchestrator function.
        /// </summary>
        /// <value>JSON-serializeable input value for the orchestrator function.</value>
        public object Input { get; set; }
    }
}
