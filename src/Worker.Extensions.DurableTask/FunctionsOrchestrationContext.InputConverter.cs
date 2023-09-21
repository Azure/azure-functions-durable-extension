// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed partial class FunctionsOrchestrationContext
{
    private abstract class InputConverter
    {
        public abstract T Get<T>();

        public static InputConverter Create(
            object? baseInput, DataConverter converter, JsonSerializerOptions jsonOptions)
        {
            return baseInput switch
            {
                JsonElement json => new JsonElementInputConverter(json, jsonOptions),
                null => NullInputConverter.Instance,
                _ => new DefaultInputConverter(baseInput, converter),
            };
        }
    }

    private class DefaultInputConverter : InputConverter
    {
        private readonly DataConverter converter;
        private readonly string serializedInput;

        public DefaultInputConverter(object input, DataConverter converter)
        {
            this.converter = converter;
            this.serializedInput = converter.Serialize(input);
        }

        public override T Get<T>()
        {
            return this.converter.Deserialize<T>(this.serializedInput);
        }
    }

    private class JsonElementInputConverter : InputConverter
    {
        private readonly JsonElement json;
        private readonly JsonSerializerOptions jsonOptions;

        public JsonElementInputConverter(JsonElement json, JsonSerializerOptions jsonOptions)
        {
            this.json = json;
            this.jsonOptions = jsonOptions;
        }

        public override T Get<T>()
        {
            return this.json.Deserialize<T>(this.jsonOptions)!;
        }
    }

    private class NullInputConverter : InputConverter
    {
        public static readonly NullInputConverter Instance = new();

        public override T Get<T>()
        {
            return default!;
        }
    }
}
