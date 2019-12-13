﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Query condition for searching the status of orchestration instances.
    /// </summary>
    public class OrchestrationStatusQueryCondition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestrationStatusQueryCondition"/> class.
        /// </summary>
        public OrchestrationStatusQueryCondition() { }

        internal OrchestrationStatusQueryCondition(EntityQuery entityQuery)
        {
            this.CreatedTimeFrom = entityQuery.LastOperationFrom;
            this.CreatedTimeTo = entityQuery.LastOperationTo;
            this.PageSize = entityQuery.PageSize;
            this.ContinuationToken = entityQuery.ContinuationToken;
            this.InstanceIdPrefix = entityQuery.EntityName;
            this.FetchInput = entityQuery.FetchInput;
        }

        /// <summary>
        /// Return orchestration instances which matches the runtimeStatus.
        /// </summary>
        public IEnumerable<OrchestrationRuntimeStatus> RuntimeStatus { get; set; }

        /// <summary>
        /// Return orchestration instances which were created after this DateTime.
        /// </summary>
        public DateTime CreatedTimeFrom { get; set; }

        /// <summary>
        /// Return orchestration instances which were created before this DateTime.
        /// </summary>
        public DateTime CreatedTimeTo { get; set; }

        /// <summary>
        /// Return orchestration instances which matches the TaskHubNames.
        /// </summary>
        public IEnumerable<string> TaskHubNames { get; set; }

        /// <summary>
        /// Number of records per one request. The default value is 100.
        /// </summary>
        public int PageSize { get; set; } = 100;

        /// <summary>
        /// ContinuationToken of the pager.
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Return orchestration instances that have this instance id prefix.
        /// </summary>
        public string InstanceIdPrefix { get; set; }

        /// <summary>
        /// If true, the query will get the input of the entity.
        /// </summary>
        public bool FetchInput { get; set; } = true;
    }
}
