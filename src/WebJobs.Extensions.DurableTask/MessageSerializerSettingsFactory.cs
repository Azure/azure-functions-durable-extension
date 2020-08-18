// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class MessageSerializerSettingsFactory : IMessageSerializerSettingsFactory
    {
        private JsonSerializerSettings jsonSerializerSettings;

        public MessageSerializerSettingsFactory()
        {
        }

        internal MessageSerializerSettingsFactory(JsonSerializerSettings jsonSerializerSettings)
        {
            this.jsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings CreateJsonSerializerSettings()
        {
            if (this.jsonSerializerSettings == null)
            {
                // The default JsonDataConverter for DTFx includes type information in JSON objects. This causes issues
                // because the type information generated from C# scripts cannot be understood by DTFx. For this reason, explicitly
                // configure the JsonDataConverter to not include CLR type information. This is also safer from a security perspective.
                this.jsonSerializerSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    DateParseHandling = DateParseHandling.None,
                };
            }

            return this.jsonSerializerSettings;
        }
    }
}
