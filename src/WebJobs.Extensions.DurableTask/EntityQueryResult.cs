using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// 
    /// </summary>
    public class EntityQueryResult
    {
        /// <summary>
        /// 
        /// </summary>
        public IReadOnlyCollection<EntityStatus> Entities { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string ContinuationToken { get; set; }
    }
}
