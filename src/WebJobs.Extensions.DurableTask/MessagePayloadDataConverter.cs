// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Text;
using DurableTask.Core.Serializing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class MessagePayloadDataConverter : JsonDataConverter
    {
        public MessagePayloadDataConverter(JsonSerializerSettings settings, bool isDefault)
            : base(settings)
        {
            this.IsDefault = isDefault;
            this.JsonSettings = settings;
            this.JsonSerializer = JsonSerializer.Create(settings);
        }

        public bool IsDefault { get; }

        internal JsonSerializerSettings JsonSettings { get; }

        internal JsonSerializer JsonSerializer { get; }

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

        public static JToken ConvertToJToken(string input)
        {
            JToken token = null;
            if (input != null)
            {
                using (var stringReader = new StringReader(input))
                using (var jsonTextReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None })
                {
                    return token = JToken.Load(jsonTextReader);
                }
            }

            return token;
        }

        public static JArray ConvertToJArray(string input)
        {
            JArray jArray = null;
            if (input != null)
            {
                using (var stringReader = new StringReader(input))
                using (var jsonTextReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None })
                {
                    jArray = JArray.Load(jsonTextReader);
                }
            }

            return jArray;
        }
    }
}
