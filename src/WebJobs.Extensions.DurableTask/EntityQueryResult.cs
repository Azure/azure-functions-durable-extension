﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The status of all entity instances with paging for a given query.
    /// </summary>
    public class EntityQueryResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityQueryResult"/> class.
        /// </summary>
        public EntityQueryResult() { }

        internal EntityQueryResult(OrchestrationStatusQueryResult orchestrationResult)
        {
            this.Entities = this.GenerateEntityStatusCollection(orchestrationResult.DurableOrchestrationState);
            this.ContinuationToken = orchestrationResult.ContinuationToken;
        }

        /// <summary>
        /// Gets or sets a collection of statuses of entity instances matching the query description.
        /// </summary>
        /// <value>A collection of entity instance status values.</value>
        public IReadOnlyCollection<DurableEntityStatus> Entities { get; set; }

        /// <summary>
        /// Gets or sets a token that can be used to resume the query with data not already returned by this query.
        /// </summary>
        /// <value>A server-generated continuation token or <c>null</c> if there are no further continuations.</value>
        public string ContinuationToken { get; set; }

        private IReadOnlyCollection<DurableEntityStatus> GenerateEntityStatusCollection(IEnumerable<DurableOrchestrationStatus> orchestrationStatuses)
        {
            var collection = new List<DurableEntityStatus>();
            foreach (DurableOrchestrationStatus orchestrationStatus in orchestrationStatuses)
            {
                collection.Add(new DurableEntityStatus(orchestrationStatus));
            }

            return collection;
        }
    }
}
