// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    internal class TestCustomConnectionsStringResolver : IConnectionStringResolver
    {
        private readonly Dictionary<string, string> connectionStrings;

        public TestCustomConnectionsStringResolver(Dictionary<string, string> connectionStrings)
        {
            this.connectionStrings = connectionStrings;
        }

        public string Resolve(string connectionStringName)
        {
            if (this.connectionStrings.TryGetValue(connectionStringName, out string value))
            {
                return value;
            }

            return null;
        }
    }
}
