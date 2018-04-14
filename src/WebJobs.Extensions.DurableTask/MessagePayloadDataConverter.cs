// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text;
using DurableTask.Core.Serializing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class MessagePayloadDataConverter : JsonDataConverter
    {
        // The default JsonDataConverter for DTFx includes type information in JSON objects. This causes issues
        // because the type information generated from C# scripts cannot be understood by DTFx. For this reason, explicitly
        // configure the JsonDataConverter to not include CLR type information. This is also safer from a security perspective.
        private static readonly JsonSerializerSettings SharedSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
        };

        // Default singleton instance
        public static readonly MessagePayloadDataConverter Default = new MessagePayloadDataConverter();

        public MessagePayloadDataConverter()
            : base(SharedSettings)
        {
        }

        /// <summary>
        /// JSON-serializes the specified object.
        /// </summary>
        public override string Serialize(object value)
        {
            return this.Serialize(value, maxSizeInKB: -1);
        }

        /// <summary>
        /// JSON-serializes the specified object and throws a <see cref="ArgumentException"/> if the
        /// resulting JSON exceeds the maximum size specified by <paramref name="maxSizeInKB"/>.
        /// </summary>
        public string Serialize(object value, int maxSizeInKB)
        {
            string serializedJson;

            JToken json = value as JToken;
            if (json != null)
            {
                serializedJson = json.ToString(Formatting.None);
            }
            else
            {
                serializedJson = base.Serialize(value);
            }

            if (maxSizeInKB > 0)
            {
                // String payloads in Azure Storage are encoded in UTF-16.
                int payloadSizeInKB = (int)(Encoding.Unicode.GetByteCount(serializedJson) / 1024.0);
                if (payloadSizeInKB > maxSizeInKB)
                {
                    throw new ArgumentException(
                        string.Format(
                            "The UTF-16 size of the JSON-serialized payload must not exceed {0:N0} KB. The current payload size is {1:N0} KB.",
                            maxSizeInKB,
                            payloadSizeInKB));
                }
            }

            return serializedJson;
        }
    }
}
