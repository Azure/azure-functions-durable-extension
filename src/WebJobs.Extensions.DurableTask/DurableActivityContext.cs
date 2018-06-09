// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// The default parameter type for activity functions.
    /// </summary>
    public class DurableActivityContext
    {
        private readonly string instanceId;
        private readonly string serializedInput;

        private JToken parsedJsonInput;
        private string serializedOutput;

        internal DurableActivityContext(string instanceId, string serializedInput)
        {
            this.instanceId = instanceId;
            this.serializedInput = serializedInput;
        }

        /// <summary>
        /// Gets the instance ID of the currently executing orchestration.
        /// </summary>
        /// <remarks>
        /// The instance ID is fixed when the parent orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be an arbitrary string value.
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
        internal string GetRawInput()
        {
            return this.serializedInput;
        }

        /// <summary>
        /// Gets the input of the current activity function instance as a <c>JToken</c>.
        /// </summary>
        /// <returns>
        /// The parsed <c>JToken</c> representation of the activity input.
        /// </returns>
        internal JToken GetInputAsJson()
        {
            if (this.serializedInput != null && this.parsedJsonInput == null)
            {
                JArray array = JArray.Parse(this.serializedInput);
                if (array?.Count != 1)
                {
                    throw new ArgumentException("The serialized input is expected to be a JSON array with one element.");
                }

                this.parsedJsonInput = array[0];
            }

            return this.parsedJsonInput;
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

            return (T)this.GetInput(typeof(T));
        }

        internal object GetInput(Type destinationType)
        {
            if (this.serializedInput == null)
            {
                return destinationType.IsValueType ?
                    Activator.CreateInstance(destinationType) :
                    null;
            }

            JToken jToken = this.GetInputAsJson();
            var value = jToken as JValue;
            if (value != null)
            {
                return value.ToObject(destinationType);
            }

            string serializedValue = jToken.ToString(Formatting.None);

            // Object inputs for out-of-proc activities are passed in their JSON-stringified form with a destination
            // type of System.String. Unfortunately, deserializing a JSON string to a string causes
            // MessagePayloadDataConverter to throw an exception. This is a workaround for that case. All other
            // inputs with destination System.String (in-proc: JSON and not JSON; out-of-proc: not-JSON) inputs with
            // destination System.String should cast to JValues and be handled above.)
            if (destinationType.Equals(typeof(string)))
            {
                return serializedValue;
            }

            return MessagePayloadDataConverter.Default.Deserialize(serializedValue, destinationType);
        }

        internal string GetSerializedOutput()
        {
            return this.serializedOutput;
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
        internal void SetOutput(object output)
        {
            if (this.serializedOutput != null)
            {
                throw new InvalidOperationException("The output has already been set of this activity instance.");
            }

            if (output != null)
            {
                JToken json = output as JToken;
                if (json != null)
                {
                    this.serializedOutput = json.ToString(Formatting.None);
                }
                else
                {
                    this.serializedOutput = MessagePayloadDataConverter.Default.Serialize(output);
                }
            }
            else
            {
                this.serializedOutput = null;
            }
        }
    }
}
