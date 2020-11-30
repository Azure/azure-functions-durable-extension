// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// This class is provides the header values as strings,
    /// instead of StringValues because StringValues lose their data when
    /// deserialized. The header value are changed to StringValues before
    /// the request is sent.
    /// </summary>
    [DataContract]
    public class TestDurableHttpRequest
    {
        public TestDurableHttpRequest(HttpMethod httpMethod, string uri = "https://www.dummy-url.com", IDictionary<string, string> headers = null, string content = null, ITokenSource tokenSource = null, TimeSpan? timeout = null)
        {
            this.HttpMethod = httpMethod;
            this.Uri = uri;
            this.Headers = headers;
            this.Content = content;
            this.TokenSource = tokenSource;
            this.Timeout = timeout;
        }

        [DataMember]
        public HttpMethod HttpMethod { get; set; }

        [DataMember]
        public string Uri { get; set; }

        [DataMember]
        public IDictionary<string, string> Headers { get; set; }

        [DataMember]
        public string Content { get; set; }

        /// <summary>
        /// Information needed to get a token for a specified service.
        /// </summary>
        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        public ITokenSource TokenSource { get; set; }

        [DataMember]
        public bool AsynchronousPatternEnabled { get; set; } = true;

        [DataMember]
        public TimeSpan? Timeout { get; set; }
    }
}