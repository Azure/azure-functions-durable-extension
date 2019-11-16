using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// 
    /// </summary>
    public class EntityQuery
    {
        /// <summary>
        /// 
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime LastOperationFrom { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 
        /// </summary>
        public DateTime LastOperationTo { get; set; } = DateTime.MaxValue;

        /// <summary>
        /// 
        /// </summary>
        public int PageSize { get; set; } = 100;

        /// <summary>
        /// 
        /// </summary>
        public string ContinuationToken { get; set; }
    }
}
