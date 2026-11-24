// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class PollingHeaderPolicy
    {
        internal static IDictionary<string, string[]>? Copy(IDictionary<string, string[]>? policy)
        {
            if (policy == null)
            {
                return null;
            }

            var copy = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string[]> entry in policy)
            {
                int authorityStart = entry.Key.IndexOf("://", StringComparison.Ordinal);
                int pathStart = authorityStart < 0 ? -1 : entry.Key.IndexOf('/', authorityStart + 3);
                if (!Uri.TryCreate(entry.Key, UriKind.Absolute, out Uri? origin)
                    || origin.Scheme != Uri.UriSchemeHttps
                    || origin.HostNameType == UriHostNameType.Unknown
                    || IsScopedIpv6Address(origin)
                    || origin.Host.IndexOf('*') >= 0
                    || origin.Port <= 0
                    || entry.Key.IndexOf('\\') >= 0
                    || entry.Key.IndexOf('@') >= 0
                    || !string.Equals(entry.Key, entry.Key.Trim(), StringComparison.Ordinal)
                    || (pathStart >= 0 && pathStart != entry.Key.Length - 1)
                    || origin.UserInfo.Length != 0
                    || origin.AbsolutePath != "/"
                    || origin.Query.Length != 0
                    || origin.Fragment.Length != 0
                    || entry.Value == null)
                {
                    throw new ArgumentException("Polling header configuration requires exact HTTPS origins and header lists.", nameof(policy));
                }

                var names = new string[entry.Value.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    string name = entry.Value[i];
                    if (string.IsNullOrEmpty(name) || name == "*")
                    {
                        throw new ArgumentException("Polling header configuration contains an invalid header name.", nameof(policy));
                    }

                    foreach (char character in name)
                    {
                        if (!(character >= 'a' && character <= 'z')
                            && !(character >= 'A' && character <= 'Z')
                            && !(character >= '0' && character <= '9')
                            && "!#$%&'*+-.^_`|~".IndexOf(character) < 0)
                        {
                            throw new ArgumentException("Polling header configuration contains an invalid header name.", nameof(policy));
                        }
                    }

                    names[i] = name;
                }

                string key;
                try
                {
                    key = GetOriginKey(origin);
                }
                catch (UriFormatException ex)
                {
                    throw new ArgumentException("Polling header configuration requires a valid hostname.", nameof(policy), ex);
                }

                if (copy.ContainsKey(key))
                {
                    throw new ArgumentException("Polling header configuration contains duplicate origins.", nameof(policy));
                }

                copy.Add(key, names);
            }

            return copy;
        }

        internal static IDictionary<string, StringValues> Select(
            IDictionary<string, StringValues>? headers,
            IDictionary<string, string[]>? policy,
            Uri destination)
        {
            var selected = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            if (headers == null || policy == null || !destination.IsAbsoluteUri
                || destination.Scheme != Uri.UriSchemeHttps || IsScopedIpv6Address(destination))
            {
                return selected;
            }

            string originKey;
            try
            {
                originKey = GetOriginKey(destination);
            }
            catch (UriFormatException)
            {
                return selected;
            }

            if (!policy.TryGetValue(originKey, out var names))
            {
                return selected;
            }

            var allowed = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var matchingHeaders = headers.Where(header =>
                allowed.Contains(header.Key)
                && !header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                && !header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
                && !header.Key.Equals("x-functions-key", StringComparison.OrdinalIgnoreCase));

            foreach (KeyValuePair<string, StringValues> header in matchingHeaders)
            {
                selected.Add(header.Key, new StringValues((string[])header.Value.ToArray().Clone()));
            }

            return selected;
        }

        private static bool IsScopedIpv6Address(Uri uri)
        {
            return uri.HostNameType == UriHostNameType.IPv6 && uri.IdnHost.IndexOf('%') >= 0;
        }

        private static string GetOriginKey(Uri uri)
        {
            string authority = uri.GetLeftPart(UriPartial.Authority);
            if (uri.HostNameType != UriHostNameType.Dns || authority.IndexOf('@') >= 0)
            {
                return authority;
            }

            string idnHost = uri.IdnHost;
            return string.Equals(uri.Host, idnHost, StringComparison.Ordinal)
                ? authority
                : new UriBuilder(uri.Scheme, idnHost, uri.Port).Uri.GetLeftPart(UriPartial.Authority);
        }
    }
}
