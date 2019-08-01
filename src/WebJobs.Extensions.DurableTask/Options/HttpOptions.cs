﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Used for Durable HTTP functionality.
    /// </summary>
    public class HttpOptions
    {
        /// <summary>
        /// Reserved name to know when a TaskActivity should be an HTTP activity.
        /// </summary>
        internal const string HttpTaskActivityReservedName = "Durable:Http:Async:Activity:Function";

        /// <summary>
        /// Gets or sets the default number of milliseconds between async HTTP status poll requests.
        /// </summary>
        public int DefaultAsyncRequestSleepTimeMilliseconds { get; set; } = 30000;
    }
}
