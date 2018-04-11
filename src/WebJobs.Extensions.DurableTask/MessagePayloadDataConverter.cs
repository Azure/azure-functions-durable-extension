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
        // The payload gets written to Azure Table Storage and to Azure Queues, which have
        // strict storage limitations (64 KB). Until we support large messages, we need to block them.
        // https://github.com/Azure/azure-functions-durable-extension/issues/79
        // We limit to 60 KB to leave room for metadata.
        private const int MaxMessagePayloadSizeInKB = 60;

        // The default JsonDataConverter for DTFx includes type information in JSON objects. This blows up when using Functions
        // because the type information generated from C# scripts cannot be understood by DTFx. For this reason, explicitly
        // configure the JsonDataConverter with default serializer settings, which don't include CLR type information.
        private static readonly JsonSerializerSettings SharedSettings = new JsonSerializerSettings();

        // Default singleton instance
        public static readonly MessagePayloadDataConverter Default = new MessagePayloadDataConverter();

        public MessagePayloadDataConverter()
            : base(SharedSettings)
        {
        }

        /// <summary>
        /// JSON-serializes the specified object. This method will throw if the maximum message payload size is exceeded.
        /// </summary>
        public override string Serialize(object value)
        {
            return this.Serialize(value, MaxMessagePayloadSizeInKB);
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

            return serializedJson;
        }
    }
}
