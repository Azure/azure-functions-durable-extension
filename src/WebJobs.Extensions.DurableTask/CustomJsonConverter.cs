// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A little helper class for defining a custom JSON converter that can convert any
    /// class <typeparamref name="T"/> that implements the conversion interface <see cref="IConversion"/>.
    /// </summary>
    /// <typeparam name="T">the class which should be converted.</typeparam>
    internal class CustomJsonConverter<T> : JsonConverter
        where T : CustomJsonConverter<T>.IConversion, new()
    {
        /// <summary>
        /// Simple interface on objects to implement custom json conversion.
        /// </summary>
        public interface IConversion
        {
            /// <summary>
            /// reads from json into this object.
            /// </summary>
            /// <param name="reader">The Newtonsoft.Json.JsonReader to read from.</param>
            /// <param name="serializer">The calling serializer.</param>
            void FromJson(JsonReader reader, JsonSerializer serializer);

            /// <summary>
            /// writes this object to json.
            /// </summary>
            /// <param name="writer">The Newtonsoft.Json.JsonWriter to write to.</param>
            /// <param name="serializer">The calling serializer.</param>
            void ToJson(JsonWriter writer, JsonSerializer serializer);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(T);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var target = (T)existingValue;
            if (target == null)
            {
                target = new T();
            }

            target.FromJson(reader, serializer);

            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var source = (T)value;
            source.ToJson(writer, serializer);
        }
    }
}
