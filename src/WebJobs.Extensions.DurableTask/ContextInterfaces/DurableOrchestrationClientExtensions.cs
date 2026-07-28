// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Extension methods for <see cref="IDurableOrchestrationClient"/>.
    /// </summary>
    public static class DurableOrchestrationClientExtensions
    {
        /// <summary>
        /// Starts a new execution of the specified orchestrator function, accepting a value-type or reference-type input.
        /// </summary>
        /// <remarks>
        /// The <see cref="IDurableOrchestrationClient"/> two-argument <c>StartNewAsync&lt;T&gt;(string, T)</c> instance
        /// method constrains <typeparamref name="T"/> to reference types, so value-type inputs (for example tuples or
        /// structs) cannot be passed to it directly. This extension method lifts that restriction without changing the
        /// public interface contract: for reference-type inputs the instance method continues to be selected (instance
        /// methods take precedence over extension methods), so existing behavior is unchanged, while value-type inputs
        /// resolve to this extension. It delegates to the three-argument <c>StartNewAsync&lt;T&gt;(string, string, T)</c>
        /// overload using an empty instance id, matching the behavior of the two-argument instance method.
        /// </remarks>
        /// <param name="client">The durable orchestration client.</param>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="input">JSON-serializable input value for the orchestrator function.</param>
        /// <typeparam name="T">The type of the input value for the orchestrator function.</typeparam>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the
        /// started orchestration instance.</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="client"/> argument is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public static Task<string> StartNewAsync<T>(this IDurableOrchestrationClient client, string orchestratorFunctionName, T input)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            return client.StartNewAsync(orchestratorFunctionName, instanceId: string.Empty, input);
        }
    }
}
