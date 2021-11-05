// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Query condition for searching the status of entity instances.
    /// </summary>
    public class EntityQuery
    {
        /// <summary>
        /// Return entity instances associated with this entity name.
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Return entity instances which had operations after this DateTime.
        /// </summary>
        public DateTime LastOperationFrom { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Return entity instances which had operations before this DateTime.
        /// </summary>
        public DateTime LastOperationTo { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of records per one request. The default value is 100.
        /// </summary>
        public int PageSize { get; set; } = 100;

        /// <summary>
        /// ContinuationToken of the pager.
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Determines whether the query will include the state of the entity.
        /// </summary>
        public bool FetchState { get; set; } = false;

        /// <summary>
        /// Determines whether the results should include recently deleted entities.
        /// </summary>
        public bool IncludeDeleted { get; set; } = false;
    }
}
