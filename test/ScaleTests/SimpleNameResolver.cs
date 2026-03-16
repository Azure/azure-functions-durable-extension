// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Simple INameResolver implementation for tests that returns the input as-is.
    /// </summary>
    internal class SimpleNameResolver : Microsoft.Azure.WebJobs.INameResolver
    {
        public string Resolve(string name)
        {
            return name;
        }
    }
}
