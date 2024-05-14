// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The default parameter type for activity functions.
    /// </summary>
    internal class DurableActivityContext : IDurableActivityContext,
#pragma warning disable 618
        DurableActivityContextBase // for v1 legacy compatibility.
#pragma warning restore 618
    {
        private readonly string functionName;
        private readonly string serializedInput;
        private readonly string instanceId;
        private readonly MessagePayloadDataConverter messageDataConverter;
        private readonly bool inputsAreArrays;
        private readonly bool rawInput;

        private JToken parsedJsonInput;
        private string serializedOutput;

        internal DurableActivityContext(DurableTaskExtension config, string instanceId, string serializedInput, string functionName)
        {
            this.messageDataConverter = config.MessageDataConverter;
            this.instanceId = instanceId;
            this.serializedInput = serializedInput;
            this.functionName = functionName;

            // Activity inputs are received differently depending on whether the DTFx orchestration context is the one scheduling the activity.
            this.inputsAreArrays =
                config.OutOfProcProtocol == OutOfProcOrchestrationProtocol.OrchestratorShim ||
                config.PlatformInformationService.GetWorkerRuntimeType() == WorkerRuntimeType.DotNetIsolated;

            // Do not manipulate JSON input when using middleware passthrough.
            this.rawInput = config.OutOfProcProtocol == OutOfProcOrchestrationProtocol.MiddlewarePassthrough;
        }

        /// <inheritdoc />
        string IDurableActivityContext.InstanceId => this.instanceId;

        /// <inheritdoc />
        string IDurableActivityContext.Name => this.functionName;

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
                string actualInput;
                if (this.inputsAreArrays)
                {
                    // The legacy behavior where the DTFx orchestration context schedules the activity results in an input that's
                    // wrapped in an array. This is unfortunately quite inefficient because it requires us to do an extra serialization
                    // round-trip in order to correctly interpret the input data.
                    object[] objectArray = this.messageDataConverter.Deserialize<object[]>(this.serializedInput);
                    if (objectArray?.Length != 1)
                    {
                        throw new ArgumentException("The serialized input is expected to be an object array with one element.");
                    }

                    actualInput = this.messageDataConverter.Serialize(objectArray[0]);
                }
                else
                {
                    // The input can be parsed as-is when not using the DTFx orchestration context.
                    actualInput = this.serializedInput;
                }

                this.parsedJsonInput = MessagePayloadDataConverter.ConvertToJToken(actualInput);
            }

            return this.parsedJsonInput;
        }

        /// <inheritdoc />
        T IDurableActivityContext.GetInput<T>()
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
            if (jToken == null)
            {
                return null;
            }

            if (!this.rawInput && jToken is JValue value)
            {
                return value.ToObject(destinationType);
            }

            string serializedValue = jToken.ToString(Formatting.None);

            if (this.rawInput) // the "modern" OOProc protocol case
            {
                return serializedValue;
            }
            else if (destinationType.Equals(typeof(string))) // legacy OOProc protocol case (JS, Python, PowerShell)
            {
                // Object/complex inputs in "legacy" out-of-proc activities are passed with a destination
                // type of System.String (to be precise, if inspected with a debugger, you'll see a stringified JSON).
                // Unfortunately, deserializing a JSON string to a string causes  MessagePayloadDataConverter to throw an exception, so the
                // return statement prevents that.
                return serializedValue;
            }

            return this.messageDataConverter.Deserialize(serializedValue, destinationType);
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
                    this.serializedOutput = this.messageDataConverter.Serialize(output);
                }
            }
            else
            {
                this.serializedOutput = null;
            }
        }
    }
}
