// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
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
                    // Intentional: preserve customer-defined exception types and their serialized object graphs.
                    // Error metadata is trusted runtime data, protected by task hub backend access controls;
                    // these settings must not be used to deserialize arbitrary untrusted request JSON.
                    // Disabling type handling or adding a fixed type allowlist would break exception propagation
                    // for existing customers. It would not replace the requirement to protect backend write access.
                    // See "Task Hub Trust Boundary and Exception Serialization" in this extension's README.md.
                    TypeNameHandling = TypeNameHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                };
            }

            return this.jsonSerializerSettings;
        }

        private class ExceptionResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);

                // Strip the TargetSite property from all exceptions
                if (typeof(Exception).IsAssignableFrom(property.DeclaringType) &&
                    property.PropertyName == nameof(Exception.TargetSite))
                {
                    property.ShouldSerialize = _ => false;
                }

                return property;
            }
        }
    }
}