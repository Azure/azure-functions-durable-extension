// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The result of a clean entity storage operation.
    /// </summary>
    public struct CleanEntityStorageResult
    {
        /// <summary>
        /// The number of orphaned locks that were removed.
        /// </summary>
        public int NumberOfOrphanedLocksRemoved;

        /// <summary>
        /// The number of entities whose metadata was removed from storage.
        /// </summary>
        public int NumberOfEmptyEntitiesRemoved;
    }
}
