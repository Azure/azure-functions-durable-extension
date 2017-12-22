// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Abstract class for parameter data for activity bindings that are scheduled by their parent orchestrations.
    /// </summary>
    public abstract class DurableActivityContextBase
    {
        private readonly string instanceId;
        protected readonly string serializedInput;

        protected string serializedOutput;

        internal DurableActivityContextBase(string instanceId, string serializedInput)
        {
            this.instanceId = instanceId;
            this.serializedInput = serializedInput;
        }

        /// <summary>
        /// Gets the instance ID of the currently executing orchestration.
        /// </summary>
        /// <remarks>
        /// The instance ID is generated and fixed when the parent orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The ID of the current orchestration instance.
        /// </value>
        public string InstanceId => this.instanceId;

        /// <summary>
        /// Returns the input of the task activity in its raw JSON string value.
        /// </summary>
        /// <returns>
        /// The raw JSON-formatted activity input as a string value.
        /// </returns>
        public string GetRawInput()
        {
            return this.serializedInput;
        }

        /// <summary>
        /// Gets the input of the current activity function instance as a <c>JToken</c>.
        /// </summary>
        /// <returns>
        /// The parsed <c>JToken</c> representation of the activity input.
        /// </returns>
        public JToken GetInputAsJson()
        {
            if (this.serializedInput == null)
            {
                return null;
            }
            else
            {
                JArray array = JArray.Parse(this.serializedInput);
                if (array?.Count != 1)
                {
                    throw new ArgumentException("The serialized input is expected to be a JSON array with one element.");
                }

                JToken token = array[0];
                return token;
            }
        }

        /// <summary>
        /// Gets the input of the current activity function as a deserialized value.
        /// </summary>
        /// <typeparam name="T">Any data contract type that matches the JSON input.</typeparam>
        /// <returns>The deserialized input value.</returns>
        public T GetInput<T>()
        {
            if (this.serializedInput == null)
            {
                return default(T);
            }

            JToken jToken = this.GetInputAsJson();
            var value = jToken as JValue;
            if (value != null)
            {
                return value.ToObject<T>();
            }

            string serializedValue = jToken.ToString(Formatting.None);
            return MessagePayloadDataConverter.Default.Deserialize<T>(serializedValue);
        }
    }
}
