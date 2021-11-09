// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// Represents a traceParent that is defined W3C TraceContext.
    /// </summary>
    public class TraceParent
    {
        /// <summary>
        /// Gets or sets the Version of the traceParent.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the TraceId of the traceParent.
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// Gets or sets the SpanId of the traceParent.
        /// </summary>
        public string SpanId { get; set; }

        /// <summary>
        /// Gets or sets the TraceFlags of the traceParent.
        /// </summary>
        public string TraceFlags { get; set; }

        /// <summary>
        /// Convert a traceParent string to TraceParent object.
        /// </summary>
        /// <param name="traceparent">string representations of traceParent.</param>
        /// <returns>TraceParent object.</returns>
        public static TraceParent FromString(string traceparent)
        {
            var exceptionString =
                $"Traceparent doesn't respect the spec. spec: {{version}}-{{traceId}}-{{spanId}}-{{traceFlags}} actual: {traceparent}";
            if (!string.IsNullOrEmpty(traceparent))
            {
                var substrings = traceparent.Split('-');
                if (substrings.Length != 4)
                {
                    throw new ArgumentException(exceptionString);
                }

                return new TraceParent
                {
                    Version = substrings[0],
                    TraceId = substrings[1],
                    SpanId = substrings[2],
                    TraceFlags = substrings[3],
                };
            }

            throw new ArgumentException(exceptionString);
        }
    }
}
