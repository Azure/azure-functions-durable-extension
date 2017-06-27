// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using DurableTask.Core.Serializing;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Parameter data for activity bindings that are scheduled by their parent orchestrations.
    /// </summary>
    public class DurableActivityContext : IActivityReturnValue
    {
        private static readonly JsonDataConverter SharedJsonConverter = DurableOrchestrationContext.SharedJsonConverter;

        private readonly string instanceId;
        private readonly string rawInput;

        private string returnValue;

        internal DurableActivityContext(string instanceId, string rawInput)
        {
            this.instanceId = instanceId;
            this.rawInput = rawInput;
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

        // Intended for use by internal callers.
        string IActivityReturnValue.ReturnValue => this.returnValue;

        // Intended for use by internal callers.
        void IActivityReturnValue.SetReturnValue(object responseValue)
        {
            this.SetOutput(responseValue);
        }

        /// <summary>
        /// Returns the input of the task activity in its raw JSON string value.
        /// </summary>
        /// <returns>
        /// The raw JSON-formatted activity input as a string value.
        /// </returns>
        public string GetRawInput()
        {
            return this.rawInput;
        }

        /// <summary>
        /// Gets the input of the current activity function instance as a <c>JToken</c>.
        /// </summary>
        /// <returns>
        /// The parsed <c>JToken</c> representation of the activity input.
        /// </returns>
        public JToken GetInputAsJson()
        {
            return this.rawInput != null ? JToken.Parse(this.rawInput) : null;
        }

        /// <summary>
        /// Gets the input of the current activity function as a deserialized value.
        /// </summary>
        /// <typeparam name="T">Any data contract type that matches the JSON input.</typeparam>
        /// <returns>The deserialized input value.</returns>
        public T GetInput<T>()
        {
            return ParseActivityInput<T>(this.rawInput);
        }

        internal static T ParseActivityInput<T>(string rawInput)
        {
            // Copied from DTFx Framework\TaskActivity.cs
            T parameter = default(T);
            JArray array = JArray.Parse(rawInput);
            if (array != null)
            {
                int parameterCount = array.Count;
                if (parameterCount > 1)
                {
                    throw new ArgumentException(
                        "Activity implementation cannot be invoked due to more than expected input parameters.  Signature mismatch.");
                }

                if (parameterCount == 1)
                {
                    JToken token = array[0];
                    var value = token as JValue;
                    if (value != null)
                    {
                        parameter = value.ToObject<T>();
                    }
                    else
                    {
                        string serializedValue = token.ToString();
                        parameter = SharedJsonConverter.Deserialize<T>(serializedValue);
                    }
                }
            }

            return parameter;
        }

        /// <summary>
        /// Sets the JSON-serializeable output of the activity function.
        /// </summary>
        /// <remarks>
        /// If this method is not called explicitly, the return value of the activity function is used as the output.
        /// </remarks>
        /// <param name="output">
        /// The JSON-serializeable value to use as the activity function output.
        /// </param>
        public void SetOutput(object output)
        {
            if (output != null)
            {
                JToken json = output as JToken;
                if (json != null)
                {
                    this.returnValue = json.ToString(Formatting.None);
                }
                else
                {
                    this.returnValue = SharedJsonConverter.Serialize(output);
                }
            }
            else
            {
                this.returnValue = null;
            }
        }
    }
}
