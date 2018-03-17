// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text;
using DurableTask.Core.Serializing;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class MessagePayloadDataConverter : JsonDataConverter
    {
        // The payload gets written to Azure Table Storage and to Azure Queues, which have
        // strict storage limitations (64 KB). Until we support large messages, we need to block them.
        // https://github.com/Azure/azure-functions-durable-extension/issues/79
        // We limit to 60 KB to leave room for metadata.
        private const int MaxPayloadSizeInKB = 60;

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

        public override string Serialize(object value)
        {
            string serializedJson = base.Serialize(value);

            // String payloads in Azure Storage are encoded in UTF-16.
            int payloadSizeInKB = (int)(Encoding.Unicode.GetByteCount(serializedJson) / 1024.0);
            if (payloadSizeInKB > MaxPayloadSizeInKB)
            {
                throw new ArgumentException(
                    string.Format(
                        "The UTF-16 size of the JSON-serialized payload must not exceed 60 KB. The current payload size is {0:N0} KB.",
                        payloadSizeInKB));
            }

            return serializedJson;
        }
    }
}
