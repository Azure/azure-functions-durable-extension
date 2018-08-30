// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestConnectionStringResolver : IConnectionStringResolver
    {
        public string Resolve(string connectionStringName)
        {
            return TestHelpers.GetStorageConnectionString();
        }
    }
}
