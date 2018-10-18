// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OrchestrationStatusQueryResult
    {
        public IEnumerable<DurableOrchestrationStatus> DurableOrchestrationState { get; set; }

        public string ContinuationToken { get; set; }
    }
}
