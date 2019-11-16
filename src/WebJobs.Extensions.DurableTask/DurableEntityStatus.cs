using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// 
    /// </summary>
    public class DurableEntityStatus
    {
        /// <summary>
        /// 
        /// </summary>
        public EntityId EntityId { get; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime LastOperationTime { get; }

        /// <summary>
        /// 
        /// </summary>
        public JToken State { get; }
    }
}
