// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Reflection;
using System.Text;
using DurableTask.Core.Serializing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class MessagePayloadDataConverter : JsonDataConverter
    {
        private MessagePayloadDataConverter messageConverter;
        private MessagePayloadDataConverter errorConverter;
        private JsonSerializer messageSerializer;

        public MessagePayloadDataConverter(IMessageSerializerSettingsFactory messageSerializerSettingsFactory, IErrorSerializerSettingsFactory errorSerializerSettingsFactory)
            : base(messageSerializerSettingsFactory.CreateJsonSerializerSettings())
        {
            this.MessageSettings = messageSerializerSettingsFactory.CreateJsonSerializerSettings();
            this.ErrorSettings = errorSerializerSettingsFactory.CreateJsonSerializerSettings();
        }

        private MessagePayloadDataConverter(JsonSerializerSettings settings)
            : base(settings)
        {
        }

        internal JsonSerializerSettings MessageSettings { get; }

        internal JsonSerializerSettings ErrorSettings { get; }

        internal MessagePayloadDataConverter MessageConverter
        {
            get
            {
                if (this.messageConverter == null)
                {
                    this.messageConverter = new MessagePayloadDataConverter(this.MessageSettings);
                }

                return this.messageConverter;
            }
        }

        internal MessagePayloadDataConverter ErrorConverter
        {
            get
            {
                if (this.errorConverter == null)
                {
                    this.errorConverter = new MessagePayloadDataConverter(this.ErrorSettings);
                }

                return this.errorConverter;
            }
        }

        internal JsonSerializer MessageSerializer
        {
            get
            {
                if (this.messageSerializer == null)
                {
                    this.messageSerializer = JsonSerializer.Create(this.MessageSettings);
                }

                return this.messageSerializer;
            }
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
