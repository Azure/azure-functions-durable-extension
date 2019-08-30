// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Response received from the HTTP request made by the Durable Function.
    /// </summary>
    public class DurableHttpResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableHttpResponse"/> class.
        /// </summary>
        /// <param name="statusCode">HTTP Status code returned from the HTTP call.</param>
        /// <param name="headers">Headers returned from the HTTP call.</param>
        /// <param name="content">Content returned from the HTTP call.</param>
        [JsonConstructor]
        public DurableHttpResponse(
            HttpStatusCode statusCode,
            IDictionary<string, StringValues> headers = null,
            string content = null)
        {
            this.StatusCode = statusCode;
            this.Headers = HttpHeadersConverter.CreateCopy(headers);
            this.Content = content;
        }

        /// <summary>
        /// Status code returned from an HTTP request.
        /// </summary>
        [JsonProperty("statusCode")]
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Headers in the response from an HTTP request.
        /// </summary>
        [JsonProperty("headers")]
        [JsonConverter(typeof(HttpHeadersConverter))]
        public IDictionary<string, StringValues> Headers { get; }

        /// <summary>
        /// Content returned from an HTTP request.
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; }

        /// <summary>
        /// Creates a DurableHttpResponse from an HttpResponseMessage.
        /// </summary>
        /// <param name="httpResponseMessage">HttpResponseMessage returned from the HTTP call.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<DurableHttpResponse> CreateDurableHttpResponseWithHttpResponseMessage(HttpResponseMessage httpResponseMessage)
        {
            DurableHttpResponse durableHttpResponse = new DurableHttpResponse(
                statusCode: httpResponseMessage.StatusCode,
                headers: CreateStringValuesHeaderDictionary(httpResponseMessage.Headers),
                content: await httpResponseMessage.Content.ReadAsStringAsync());

            return durableHttpResponse;
        }

        private static IDictionary<string, StringValues> CreateStringValuesHeaderDictionary(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
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
    }
}