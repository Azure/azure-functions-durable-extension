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

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class TaskHttpActivityShim : TaskActivity
    {
        private readonly HttpClient httpClient;
        private readonly DurableTaskExtension config;

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

            return JsonConvert.SerializeObject(durableHttpResponse);
        }

        private async Task<HttpRequestMessage> ReconstructHttpRequestMessage(string serializedRequest)
        {
            // DeserializeObject deserializes into a List and then the first element
            // of that list is the DurableHttpRequest
            IList<DurableHttpRequest> input = JsonConvert.DeserializeObject<IList<DurableHttpRequest>>(serializedRequest);
            DurableHttpRequest durableHttpRequest = input.First();

            string contentType = "";
            HttpRequestMessage requestMessage = new HttpRequestMessage(durableHttpRequest.Method, durableHttpRequest.Uri);
            if (durableHttpRequest.Headers != null)
            {
                foreach (KeyValuePair<string, StringValues> entry in durableHttpRequest.Headers)
                {
                    if (entry.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
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

                if (string.Equals(contentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
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