// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TestConnectionStringResolver : IConnectionStringResolver
    {
        private INameResolver nameResolver;

        public TestConnectionStringResolver(INameResolver resolver = null)
        {
            this.nameResolver = resolver;
        }

        public string Resolve(string connectionStringName)
        {
            string result = this.nameResolver?.Resolve(connectionStringName);
            return result ?? TestHelpers.GetStorageConnectionString();
        }
    }
}
