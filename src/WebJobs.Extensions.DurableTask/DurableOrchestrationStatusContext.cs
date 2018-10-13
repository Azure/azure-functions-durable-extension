// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    // TODO add comments
    public class DurableOrchestrationStatusContext
    {
        // TODO add comments
        public IEnumerable<DurableOrchestrationStatus> DurableOrchestrationStatuses { get; set; }

        // TODO add comments
        public string ContinuationToken { get; set; }
    }
}
