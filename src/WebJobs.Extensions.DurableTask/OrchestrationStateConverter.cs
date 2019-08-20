// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Class with static helper methods for coordinating orchestration state from DurableTask.Core types to
    /// Webjobs.Extensions.DurableTask types.
    /// </summary>
    public static class OrchestrationStateConverter
    {
        private static readonly JValue NullJValue = JValue.CreateNull();

        /// <summary>
        /// Converts an object of the DurableTask.Core type <see cref="OrchestrationState"/> to
        /// the Microsoft.Azure.Webjobs.Extensions.DurableTask type <see cref="DurableOrchestrationStatus"/>.
        /// Helpful for custom implementations of storage providers.
        /// </summary>
        /// <param name="orchestrationState">The state object to convert to a status.</param>
        /// <param name="historyArray">The array of history events.</param>
        /// <returns>The orchestration state as a status response.</returns>
        public static DurableOrchestrationStatus ConvertOrchestrationStateToStatus(OrchestrationState orchestrationState, JArray historyArray = null)
        {
            return new DurableOrchestrationStatus
            {
                Name = orchestrationState.Name,
                InstanceId = orchestrationState.OrchestrationInstance.InstanceId,
                CreatedTime = orchestrationState.CreatedTime,
                LastUpdatedTime = orchestrationState.LastUpdatedTime,
                RuntimeStatus = (OrchestrationRuntimeStatus)orchestrationState.OrchestrationStatus,
                CustomStatus = ParseToJToken(orchestrationState.Status),
                Input = ParseToJToken(orchestrationState.Input),
                Output = ParseToJToken(orchestrationState.Output),
                History = historyArray,
            };
        }

        internal static JToken ParseToJToken(string value)
        {
            if (value == null)
            {
                return NullJValue;
            }

            // Ignore whitespace
            value = value.Trim();
            if (value.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                return JToken.Parse(value);
            }
            catch (JsonReaderException)
            {
                // Return the raw string value as the fallback. This is common in terminate scenarios.
                return value;
            }
        }
    }
}
