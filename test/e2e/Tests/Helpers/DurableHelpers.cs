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
    internal static OrchestrationStatusDetails GetRunningOrchestrationDetails(string statusQueryGetUri)
    {
        var statusQueryResponse = _httpClient.GetAsync(statusQueryGetUri);

        string? statusQueryResponseString = statusQueryResponse.Result.Content.ReadAsStringAsync().Result;

        return new OrchestrationStatusDetails(statusQueryResponseString);
    }
}
