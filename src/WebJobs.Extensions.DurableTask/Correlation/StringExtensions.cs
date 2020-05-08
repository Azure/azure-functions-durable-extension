// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// StringExtensions are the extension method parse string into <see cref="TraceParent"></see> object.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Convert a traceParent string to TraceParent object.
        /// </summary>
        /// <param name="traceparent">string representations of traceParent.</param>
        /// <returns>TraceParent object.</returns>
        public static TraceParent ToTraceParent(this string traceparent)
        {
            if (!string.IsNullOrEmpty(traceparent))
            {
                var substrings = traceparent.Split('-');
                if (substrings.Length != 4)
                {
                    throw new ArgumentException($"Traceparent doesn't respect the spec. spec: {{version}}-{{traceId}}-{{spanId}}-{{traceFlags}} actual: {traceparent}");
                }

                return new TraceParent
                {
                    Version = substrings[0],
                    TraceId = substrings[1],
                    SpanId = substrings[2],
                    TraceFlags = substrings[3],
                };
            }

            return null;
        }
    }
}
