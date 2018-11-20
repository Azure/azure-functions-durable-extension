// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class HttpRequestMessageExtensions
    {
        public static HttpResponseMessage CreateResponse(this HttpRequestMessage request, HttpStatusCode statusCode)
        {
            return new HttpResponseMessage
            {
                RequestMessage = request,
                StatusCode = statusCode,
            };
        }

        public static HttpResponseMessage CreateResponse<T>(this HttpRequestMessage request, HttpStatusCode statusCode, T value)
        {
            return new HttpResponseMessage
            {
                Content = new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json"),
                RequestMessage = request,
                StatusCode = statusCode,
            };
        }

        public static HttpResponseMessage CreateErrorResponse(this HttpRequestMessage request, HttpStatusCode statusCode, string message)
        {
            var error = new { Message = message };

            return new HttpResponseMessage
            {
                Content = new StringContent(JsonConvert.SerializeObject(error), Encoding.UTF8, "application/json"),
                RequestMessage = request,
                StatusCode = statusCode,
            };
        }

        public static HttpResponseMessage CreateErrorResponse(this HttpRequestMessage request, HttpStatusCode statusCode, string message, Exception e)
        {
            var error = new
            {
                Message = message,
                ExceptionMessage = e.Message,
                ExceptionType = e.GetType().FullName,
                StackTrace = e.StackTrace,
            };

            return new HttpResponseMessage
            {
                Content = new StringContent(JsonConvert.SerializeObject(error), Encoding.UTF8, "application/json"),
                RequestMessage = request,
                StatusCode = statusCode,
            };
        }

        public static NameValueCollection GetQueryNameValuePairs(this HttpRequestMessage request)
        {
            var values = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            var s = request.RequestUri.Query;
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
                    name = string.Empty;
                    value = s.Substring(si, i - si).Trim();
                }

                // add name / value pair to the collection (nulls are okay for either)
                values.Add(
                    WebUtility.UrlDecode(name),
                    WebUtility.UrlDecode(value));

                // trailing '&'
                if (i == l - 1 && s[i] == separator)
                {
                    values.Add(string.Empty, string.Empty);
                }

                i++;
            }

            return values;
        }
    }
}
