// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E
{
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

        public static async Task<bool> InvokeHttpTrigger(string functionName, string queryString, HttpStatusCode expectedStatusCode, string expectedMessage, int expectedCode = 0)
        {
            string uri = $"{Constants.FunctionsHostUrl}/api/{functionName}{queryString}";
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                var response = await GetResponseMessage(request);

                Console.WriteLine(
                    $"InvokeHttpTrigger: {functionName}{queryString} : {response.StatusCode} : {response.ReasonPhrase}");
                if (expectedStatusCode != response.StatusCode && expectedCode != (int)response.StatusCode)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(expectedMessage))
                {
                    string actualMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(
                        $"InvokeHttpTrigger: expectedMessage : {expectedMessage}, actualMessage : {actualMessage}");
                    return actualMessage.Contains(expectedMessage);
                }

                return true;
            }
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
            using (var httpClient = new HttpClient())
            {
                response = await httpClient.SendAsync(request);
            }

            return response;
        }
    }
}
