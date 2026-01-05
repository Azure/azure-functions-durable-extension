// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ErrorSerializerSettingsFactory : IErrorSerializerSettingsFactory
    {
        private JsonSerializerSettings jsonSerializerSettings;

        public ErrorSerializerSettingsFactory()
        {
        }

        internal ErrorSerializerSettingsFactory(JsonSerializerSettings jsonSerializerSettings)
        {
            this.jsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings CreateJsonSerializerSettings()
        {
            if (this.jsonSerializerSettings == null)
            {
                this.jsonSerializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new ExceptionResolver(),
                    TypeNameHandling = TypeNameHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

                    // Limit the serialization depth to prevent StackOverflowException
                    // when serializing exceptions with complex/deeply nested structures.
                    // When this depth is exceeded, a JsonSerializationException is thrown
                    // which can be caught by the caller (unlike StackOverflowException).
                    MaxDepth = 64,
                };
            }

            return this.jsonSerializerSettings;
        }

        private class ExceptionResolver : DefaultContractResolver
        {
            // These are the well-known safe properties from System.Exception that we want to serialize.
            // Other properties (especially from derived exception types like CsvHelper.FieldValidationException)
            // may contain complex object graphs that can cause StackOverflowException during serialization.
            private static readonly HashSet<string> AllowedExceptionProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                nameof(Exception.Message),
                nameof(Exception.StackTrace),
                nameof(Exception.Source),
                nameof(Exception.HelpLink),
                nameof(Exception.HResult),
                nameof(Exception.Data),
                nameof(Exception.InnerException),
            };

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);

                // For Exception types and their derived classes, only serialize properties
                // that are known to be safe. This prevents serialization of complex properties
                // like CsvHelper's Context/ReadingContext that can cause StackOverflowException.
                if (typeof(Exception).IsAssignableFrom(property.DeclaringType))
                {
                    if (!AllowedExceptionProperties.Contains(property.PropertyName))
                    {
                        property.ShouldSerialize = _ => false;
                    }
                }

                return property;
            }
        }
    }
}