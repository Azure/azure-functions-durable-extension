// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class OrchestratorFunctionUnavailableException : ArgumentException
    {
        internal OrchestratorFunctionUnavailableException(string message)
            : base(message)
        {
        }
    }
}
