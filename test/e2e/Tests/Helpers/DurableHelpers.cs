// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Nodes;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

internal class DurableHelpers
{
    internal static string ParseStatusQueryGetUri(HttpResponseMessage invocationStartResponse)
    {
        string? responseString = invocationStartResponse.Content?.ReadAsStringAsync().Result;
        if (string.IsNullOrEmpty(responseString))
        {
            return string.Empty;
        }
        JsonNode? responseJsonNode = JsonNode.Parse(responseString);
        if (responseJsonNode == null)
        {
            return string.Empty;
        }

        string? statusQueryGetUri = responseJsonNode["StatusQueryGetUri"]?.GetValue<string>();
        return statusQueryGetUri ?? string.Empty;
    }
    internal static string GetRuntimeStatus(string statusQueryGetUri)
    {
        HttpClient client = new HttpClient();
        var statusQueryResponse = client.GetAsync(statusQueryGetUri);

        string? statusQueryResponseString = statusQueryResponse.Result.Content.ReadAsStringAsync().Result;
        JsonNode? statusQueryJsonNode = JsonNode.Parse(statusQueryResponseString);
        if (statusQueryJsonNode == null)
        {
            return string.Empty;
        }
        return statusQueryJsonNode["runtimeStatus"]?.GetValue<string>() ?? string.Empty;
    }
}
