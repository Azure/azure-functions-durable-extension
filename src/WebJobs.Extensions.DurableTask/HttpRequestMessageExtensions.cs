// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class HttpRequestMessageExtensions
    {
        public static HttpResponseMessage CreateResponse<T>(this HttpRequestMessage request, HttpStatusCode statusCode, T value)
        {
            return new HttpResponseMessage
            {
                Content = new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json"),
                RequestMessage = request,
                StatusCode = statusCode,
            };
        }

        public static HttpResponse CreateResponse(this HttpRequest request, HttpStatusCode statusCode)
        {
            DefaultHttpResponse defaultHttpResponse =
                new DefaultHttpResponse(request.HttpContext)
                {
                    StatusCode = (int)statusCode,
                };
            return defaultHttpResponse;
        }

        public static async Task<HttpResponse> CreateResponse<T>(this HttpRequest request, HttpStatusCode statusCode, T value)
        {
            DefaultHttpResponse defaultHttpResponse =
                new DefaultHttpResponse(request.HttpContext)
                {
                    StatusCode = (int)statusCode,
                    ContentType = "application/json",
                };
            defaultHttpResponse.Body = new MemoryStream();
            await defaultHttpResponse.WriteAsync(JsonConvert.SerializeObject(value));
            return defaultHttpResponse;
        }

        public static async Task<HttpResponse> CreateErrorResponse(this HttpRequest request, HttpStatusCode statusCode, string message)
        {
            var error = new { Message = message };

            DefaultHttpResponse defaultHttpResponse =
                new DefaultHttpResponse(request.HttpContext)
                {
                    StatusCode = (int)statusCode,
                    ContentType = "application/json",
                };
            defaultHttpResponse.Body = new MemoryStream();
            await defaultHttpResponse.WriteAsync(JsonConvert.SerializeObject(error));
            return defaultHttpResponse;
        }

        public static async Task<HttpResponse> CreateErrorResponse(this HttpRequest request, HttpStatusCode statusCode, string message, Exception e)
        {
            var error = new
            {
                Message = message,
                ExceptionMessage = e.Message,
                ExceptionType = e.GetType().FullName,
                StackTrace = e.StackTrace,
            };

            DefaultHttpResponse defaultHttpResponse =
                new DefaultHttpResponse(request.HttpContext)
                {
                    StatusCode = (int)statusCode,
                    ContentType = "application/json",
                };
            defaultHttpResponse.Body = new MemoryStream();
            await defaultHttpResponse.WriteAsync(JsonConvert.SerializeObject(error));
            return defaultHttpResponse;
        }

        public static NameValueCollection GetQueryNameValuePairs(this HttpRequest request)
        {
            var values = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            var s = request.QueryString.ToString();
            var separator = '&';

            int l = (s != null) ? s.Length : 0;
            int i = 1; // skip the leading '?' character

            while (i < l)
            {
                // find next & while noting first = on the way (and if there are more)
                int si = i;
                int ti = -1;

                while (i < l)
                {
                    char ch = s[i];

                    if (ch == '=')
                    {
                        if (ti < 0)
                        {
                            ti = i;
                        }
                    }
                    else if (ch == separator) // e.g. '&' or ';'
                    {
                        break;
                    }

                    i++;
                }

                // extract the name / value pair
                string name = null;
                string value = null;

                if (ti >= 0)
                {
                    name = s.Substring(si, ti - si).Trim();
                    value = s.Substring(ti + 1, i - ti - 1).Trim();
                }
                else
                {
                    value = s.Substring(si, i - si).Trim();
                }

                // add name / value pair to the collection (nulls are okay for either)
                values.Add(
                    WebUtility.UrlDecode(name),
                    WebUtility.UrlDecode(value));

                // trailing '&'
                if (i == l - 1 && s[i] == separator)
                {
                    values.Add(null, string.Empty);
                }

                i++;
            }

            return values;
        }
    }
}
