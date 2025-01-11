// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

class HttpHelpers
{
    public static async Task<HttpResponseMessage> InvokeHttpTrigger(string functionName, string queryString = "")
    {
        // Basic http request
        using HttpRequestMessage request = GetTestRequest(functionName, queryString);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        return await GetResponseMessage(request);
    }

    public static async Task<HttpResponseMessage> InvokeHttpTriggerWithBody(string functionName, string body, string mediaType)
    {
        HttpRequestMessage request = GetTestRequest(functionName);
        request.Content = new StringContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        return await GetResponseMessage(request);
    }

    private static HttpRequestMessage GetTestRequest(string functionName, string queryString = "")
    {
        return new HttpRequestMessage
        {
            RequestUri = new Uri($"{Constants.FunctionsHostUrl}/api/{functionName}{queryString}"),
            Method = HttpMethod.Post
        };
    }

    private static async Task<HttpResponseMessage> GetResponseMessage(HttpRequestMessage request)
    {
        HttpResponseMessage? response = null;
        using var httpClient = new HttpClient();
        response = await httpClient.SendAsync(request);

        return response;
    }
}
