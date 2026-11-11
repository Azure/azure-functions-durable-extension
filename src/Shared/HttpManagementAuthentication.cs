// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Net;

namespace Microsoft.Azure.Functions.DurableTask
{
    internal static class HttpManagementAuthentication
    {
        internal static string GetQueryParameters(string? query, out IReadOnlyDictionary<string, string> requiredHeaders)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var parameters = new List<string>();
            foreach (string parameter in (query ?? string.Empty).TrimStart('?').Split('&'))
            {
                if (parameter.Length == 0)
                {
                    continue;
                }

                int separator = parameter.IndexOf('=');
                string name = separator < 0 ? parameter : parameter.Substring(0, separator);
                if (string.Equals(WebUtility.UrlDecode(name), "code", StringComparison.OrdinalIgnoreCase))
                {
                    if (!headers.ContainsKey("x-functions-key") && separator >= 0)
                    {
                        headers.Add("x-functions-key", WebUtility.UrlDecode(parameter.Substring(separator + 1)));
                    }
                }
                else
                {
                    parameters.Add(parameter);
                }
            }

            requiredHeaders = headers;
            return string.Join("&", parameters);
        }
    }
}
