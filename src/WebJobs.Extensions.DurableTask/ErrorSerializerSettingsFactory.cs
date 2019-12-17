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