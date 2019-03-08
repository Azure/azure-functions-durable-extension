﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The status of all orchestration instances with paging for a given query.
    /// </summary>
    public class OrchestrationStatusQueryResult
    {
        /// <summary>
        /// A collection of statuses of orchestration instances matching the query description.
        /// </summary>
        public IEnumerable<DurableOrchestrationStatus> DurableOrchestrationState { get; set; }

        /// <summary>
        /// A token that can be used to resume the query with data not already returned by this query.
        /// </summary>
        public string ContinuationToken { get; set; }
    }
}
