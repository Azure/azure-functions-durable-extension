// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The status of all entity instances with paging for a given query.
    /// </summary>
    public class EntityQueryResult
    {
        internal EntityQueryResult() { }

        internal EntityQueryResult(OrchestrationStatusQueryResult orchestrationResult, bool includeDeleted)
        {
            if (includeDeleted)
            {
                this.Entities = orchestrationResult.DurableOrchestrationState
                    .Select(status => new DurableEntityStatus(status))
                    .ToList();
            }
            else
            {
                this.Entities = orchestrationResult.DurableOrchestrationState
                    .Where(status => status?.CustomStatus is JObject jobject
                            && jobject != null
                            && jobject.TryGetValue("entityExists", out var s)
                            && s.Type == JTokenType.Boolean
                            && (bool)s)
                    .Select(status => new DurableEntityStatus(status))
                    .ToList();
            }

            this.ContinuationToken = orchestrationResult.ContinuationToken;
        }

        /// <summary>
        /// Gets or sets a collection of statuses of entity instances matching the query description.
        /// </summary>
        /// <value>A collection of entity instance status values.</value>
        public IEnumerable<DurableEntityStatus> Entities { get; set; }

        /// <summary>
        /// Gets or sets a token that can be used to resume the query with data not already returned by this query.
        /// </summary>
        /// <value>A server-generated continuation token or <c>null</c> if there are no further continuations.</value>
        public string ContinuationToken { get; set; }
    }
}
