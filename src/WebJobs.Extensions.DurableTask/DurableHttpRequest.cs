// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
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
            this.Headers = CreateDictionaryCopy(headers);
            this.Content = content;
            this.TokenSource = tokenSource;
            this.AsynchronousPatternEnabled = asynchronousPatternEnabled;
        }

        internal DurableHttpRequest(JObject jObject)
        {
            this.Method = jObject["Method"].ToObject<HttpMethod>();
            this.Uri = jObject["Uri"].ToObject<Uri>();

            Dictionary<string, StringValues> headerDictStringValues = new Dictionary<string, StringValues>();
            Dictionary<string, IEnumerable<string>> headersDictEnumerable = jObject["Headers"].ToObject<Dictionary<string, IEnumerable<string>>>();
            foreach (var header in headersDictEnumerable)
            {
                string key = header.Key;
                string[] headerValues = header.Value.ToArray<string>();
                StringValues values = new StringValues(headerValues);
                headerDictStringValues.Add(key, values);
            }

            this.Headers = headerDictStringValues;

            this.Content = jObject["Content"].Value<string>();

            JsonSerializerSettings serializer = new JsonSerializerSettings();
            serializer.TypeNameHandling = TypeNameHandling.Auto;
            string tokenSource = JsonConvert.SerializeObject(jObject["TokenSource"], serializer);

            this.TokenSource = JsonConvert.DeserializeObject<ITokenSource>(tokenSource, serializer);
            this.AsynchronousPatternEnabled = jObject["AsynchronousPatternEnabled"].Value<bool>();
        }

        /// <summary>
        /// HttpMethod used in the HTTP request made by the Durable Function.
        /// </summary>
        public HttpMethod Method { get; }

        /// <summary>
        /// Uri used in the HTTP request made by the Durable Function.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Headers passed with the HTTP request made by the Durable Function.
        /// </summary>
        public IDictionary<string, StringValues> Headers { get; }

        /// <summary>
        /// Content passed with the HTTP request made by the Durable Function.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Information needed to get a token for a specified service.
        /// </summary>
        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        public ITokenSource TokenSource { get; }

        /// <summary>
        /// Specifies whether the Durable HTTP APIs should automatically
        /// handle the asynchronous HTTP pattern.
        /// </summary>
        public bool AsynchronousPatternEnabled { get; } = true;

        private static Dictionary<string, StringValues> CreateDictionaryCopy(IDictionary<string, StringValues> headers)
        {
            Dictionary<string, StringValues> newDictionary = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, StringValues> pair in headers)
            {
                newDictionary.Add(pair.Key, pair.Value);
            }

            return newDictionary;
        }
    }
}
