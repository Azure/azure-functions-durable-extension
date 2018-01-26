// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EventIndexDateMapping
    {
        public int Index { get; set; }

        public DateTime Date { get; set; }
    }
}
