// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Test helper implementation of INameResolver interface.
    /// </summary>
    internal class SimpleNameResolver : INameResolver
    {
        private readonly Dictionary<string, string> values;

        public SimpleNameResolver()
        {
            this.values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public SimpleNameResolver(Dictionary<string, string> values)
            : this()
        {
            foreach (var pair in values)
            {
                this.values[pair.Key] = pair.Value;
            }
        }

        public string Resolve(string name)
        {
            if (this.values == null)
            {
                return null;
            }

            return this.values.TryGetValue(name, out string result) ? result : null;
        }
    }
}