// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed class FunctionsPayloadMetadataTransformer : IFunctionMetadataTransformer
{
    public string Name => nameof(FunctionsPayloadMetadataTransformer);

    public void Transform(IList<IFunctionMetadata> original)
    {
        if (original is null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        foreach (IFunctionMetadata metadata in original)
        {
            if (metadata.RawBindings is not { } bindings)
            {
                continue;
            }

            for (int index = 0; index < bindings.Count; index++)
            {
                JsonObject binding = JsonNode.Parse(bindings[index]) as JsonObject
                    ?? throw new JsonException("Function binding metadata must be a JSON object.");
                if (string.Equals((string?)binding["type"], "activityTrigger", StringComparison.OrdinalIgnoreCase)
                    && string.Equals((string?)binding["dataType"], "Binary", StringComparison.OrdinalIgnoreCase))
                {
                    // Let the worker hydrate references before decoding byte[] arguments.
                    binding["dataType"] = "String";
                    bindings[index] = binding.ToJsonString();
                }
            }
        }
    }
}
