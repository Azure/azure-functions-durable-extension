// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Nodes;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

internal class DurableHelpers
{
    static readonly HttpClient _httpClient = new HttpClient();

    internal class OrchestrationStatusDetails
    {
        public string RuntimeStatus { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public DateTime LastUpdatedTime { get; set; }
        public OrchestrationStatusDetails(string statusQueryResponse)
        {
            JsonNode? statusQueryJsonNode = JsonNode.Parse(statusQueryResponse);
            if (statusQueryJsonNode == null)
            {
                return;
            }
            this.RuntimeStatus = statusQueryJsonNode["runtimeStatus"]?.GetValue<string>() ?? string.Empty;
            this.Input = statusQueryJsonNode["input"]?.ToString() ?? string.Empty;
            this.Output = statusQueryJsonNode["output"]?.ToString() ?? string.Empty;
            this.CreatedTime = DateTime.Parse(statusQueryJsonNode["createdTime"]?.GetValue<string>() ?? string.Empty).ToUniversalTime();
            this.LastUpdatedTime = DateTime.Parse(statusQueryJsonNode["lastUpdatedTime"]?.GetValue<string>() ?? string.Empty).ToUniversalTime();
        }
    }

    internal static async Task<string> ParseStatusQueryGetUriAsync(HttpResponseMessage invocationStartResponse)
    {
        string? responseString = await invocationStartResponse.Content.ReadAsStringAsync();
        return TokenizeAndGetValueFromKeyAsString(responseString, "StatusQueryGetUri");
    }

    internal static async Task<string> ParseInstanceIdAsync(HttpResponseMessage invocationStartResponse)
    {
        string? responseString = await invocationStartResponse.Content.ReadAsStringAsync();
        return TokenizeAndGetValueFromKeyAsString(responseString, "Id");
    }

    internal static async Task<OrchestrationStatusDetails> GetRunningOrchestrationDetailsAsync(string statusQueryGetUri)
    {
        var statusQueryResponse = await _httpClient.GetAsync(statusQueryGetUri);

        string? statusQueryResponseString = await statusQueryResponse.Content.ReadAsStringAsync();

        return new OrchestrationStatusDetails(statusQueryResponseString);
    }

    private static string TokenizeAndGetValueFromKeyAsString(string? json, string key)
    {
        if (string.IsNullOrEmpty(json))
        {
            return string.Empty;
        }
        JsonNode? responseJsonNode = JsonNode.Parse(json);
        if (responseJsonNode == null)
        {
            return string.Empty;
        }

        string? statusQueryGetUri = responseJsonNode[key]?.GetValue<string>();
        return statusQueryGetUri ?? string.Empty;
    }
}
