// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Parameter data for activity bindings that are scheduled by their parent orchestrations.
    /// </summary>
    public class DurableActivityContext : DurableActivityContextBase
    {
        internal DurableActivityContext(string instanceId, string serializedInput) : base(
            instanceId,
            serializedInput)
        {
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
