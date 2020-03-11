using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    public static class StringExtensions
    {
        public static TraceParent ToTraceParent(this string traceparent)
        {
            if (!string.IsNullOrEmpty(traceparent))
            {
                var substrings = traceparent.Split('-');
                if (substrings.Length != 4)
                {
                    throw new ArgumentException($"Traceparent doesn't respect the spec. {traceparent}");
                }

                return new TraceParent
                {
                    Version = substrings[0],
                    TraceId = substrings[1],
                    SpanId = substrings[2],
                    TraceFlags = substrings[3]
                };
            }

            return null;
        }
    }
}
