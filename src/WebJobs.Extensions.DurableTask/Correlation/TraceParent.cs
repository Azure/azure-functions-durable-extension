using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    public class TraceParent
    {
        public string Version { get; set; }

        public string TraceId { get; set; }

        public string SpanId { get; set; }

        public string TraceFlags { get; set; }
    }
}
