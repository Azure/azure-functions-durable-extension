// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class TaskHttpActivityShim : TaskActivity
    {
        private readonly HttpClient httpClient;
        private readonly DurableTaskExtension config;
        private static JsonSerializerSettings serializerSettings = CreateDurableHttpResponseSerializerSettings();

        public TaskHttpActivityShim(
            DurableTaskExtension config,
            HttpClient httpClientFactory)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.httpClient = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public override string Run(TaskContext context, string input)
        {
            // This won't get called as long as we've implemented RunAsync.
            throw new NotImplementedException();
        }

        public async override Task<string> RunAsync(TaskContext context, string rawInput)
        {
            HttpRequestMessage requestMessage = await this.ReconstructHttpRequestMessage(rawInput);
            HttpResponseMessage response = await this.httpClient.SendAsync(requestMessage);
            DurableHttpResponse durableHttpResponse = await DurableHttpResponse.CreateDurableHttpResponseWithHttpResponseMessage(response);

            return MessagePayloadDataConverter.HttpConverter.Serialize(value: durableHttpResponse, formatted: true);
        }

        private static JsonSerializerSettings CreateDurableHttpResponseSerializerSettings()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
            serializerSettings.TypeNameHandling = TypeNameHandling.Objects;
            serializerSettings.Converters.Add(new DurableHttpRequestJsonConverter());
            return serializerSettings;
        }

        private static async Task<DurableHttpResponse> CopyDurableHttpResponseAsync(HttpResponseMessage response)
        {
            DurableHttpResponse durableHttpResponse = new DurableHttpResponse(
                statusCode: response.StatusCode,
                headers: CreateStringValuesHeaderDictionary(response.Headers),
                content: await response.Content.ReadAsStringAsync());

            return durableHttpResponse;
        }

        internal static IDictionary<string, StringValues> CreateStringValuesHeaderDictionary(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            IDictionary<string, StringValues> newHeaders = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    newHeaders[header.Key] = new StringValues(header.Value.ToArray());
                }
            }

            return newHeaders;
        }

        private async Task<HttpRequestMessage> ReconstructHttpRequestMessage(string serializedRequest)
        {
            // DeserializeObject deserializes into a List and then the first element
            // of that list is the serialized DurableHttpRequest
            IList<string> input = JsonConvert.DeserializeObject<List<string>>(serializedRequest, serializerSettings);
            string durableHttpRequestString = input.First();

            DurableHttpRequest durableHttpRequest = JsonConvert.DeserializeObject<DurableHttpRequest>(durableHttpRequestString, serializerSettings);

            string contentType = "";
            HttpRequestMessage requestMessage = new HttpRequestMessage(durableHttpRequest.Method, durableHttpRequest.Uri);
            if (durableHttpRequest.Headers != null)
            {
                foreach (KeyValuePair<string, StringValues> entry in durableHttpRequest.Headers)
                {
                    if (entry.Key == "Content-Type")
                    {
                        foreach (string value in entry.Value)
                        {
                            if (value.Contains("multipart"))
                            {
                                throw new FunctionFailedException("Multipart content is not supported with CallHttpAsync.");
                            }
                            else
                            {
                                contentType = value;
                            }
                        }
                    }
                    else
                    {
                        requestMessage.Headers.Add(entry.Key, (IEnumerable<string>)entry.Value);
                    }
                }
            }

            if (durableHttpRequest.Content != null)
            {
                if (contentType == "")
                {
                    contentType = "text/plain";
                }

                if (contentType == "application/x-www-form-urlencoded")
                {
                    requestMessage.Content = new StringContent(durableHttpRequest.Content, Encoding.UTF8, contentType);
                }
                else
                {
                    requestMessage.Content = new StringContent(durableHttpRequest.Content);
                    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }
            }

            if (durableHttpRequest.TokenSource != null)
            {
                string accessToken = await durableHttpRequest.TokenSource.GetTokenAsync();
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return requestMessage;
        }
    }
}