﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Request used to make an HTTP call through Durable Functions.
    /// </summary>
    public class DurableHttpRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableHttpRequest"/> class.
        /// </summary>
        /// <param name="method">Method used for HTTP request.</param>
        /// <param name="uri">Uri used to make the HTTP request.</param>
        /// <param name="headers">Headers added to the HTTP request.</param>
        /// <param name="content">Content added to the body of the HTTP request.</param>
        /// <param name="tokenSource">AAD authentication attached to the HTTP request.</param>
        /// <param name="asynchronousPatternEnabled">Specifies whether the DurableHttpRequest should handle the asynchronous pattern.</param>
        public DurableHttpRequest(
            HttpMethod method,
            Uri uri,
            IDictionary<string, StringValues> headers = null,
            string content = null,
            ITokenSource tokenSource = null,
            bool asynchronousPatternEnabled = true)
        {
            this.Method = method;
            this.Uri = uri;
            this.Headers = HttpHeadersConverter.CreateCopy(headers);
            this.Content = content;
            this.TokenSource = tokenSource;
            this.AsynchronousPatternEnabled = asynchronousPatternEnabled;
        }

        /// <summary>
        /// HttpMethod used in the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("method")]
        [JsonConverter(typeof(HttpMethodConverter))]
        public HttpMethod Method { get; }

        /// <summary>
        /// Uri used in the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("uri")]
        public Uri Uri { get; }

        /// <summary>
        /// Headers passed with the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("headers")]
        [JsonConverter(typeof(HttpHeadersConverter))]
        public IDictionary<string, StringValues> Headers { get; }

        /// <summary>
        /// Content passed with the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; }

        /// <summary>
        /// Information needed to get a token for a specified service.
        /// </summary>
        [JsonProperty("tokenSource", TypeNameHandling = TypeNameHandling.Auto)]
        public ITokenSource TokenSource { get; }

        /// <summary>
        /// Specifies whether the Durable HTTP APIs should automatically
        /// handle the asynchronous HTTP pattern.
        /// </summary>
        [JsonProperty("asynchronousPatternEnabled")]
        public bool AsynchronousPatternEnabled { get; }

        private class HttpMethodConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(HttpMethod);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    return new HttpMethod((string)JToken.Load(reader));
                }

                // Default for JSON that's either missing or not understood
                return HttpMethod.Get;
            }

            public override void WriteJson(
                JsonWriter writer,
                object value,
                JsonSerializer serializer)
            {
                HttpMethod method = (HttpMethod)value ?? HttpMethod.Get;
                writer.WriteValue(method.ToString());
            }
        }
    }
}
