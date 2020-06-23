// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class UnconstructibleClass
    {
        public UnconstructibleClass()
        {
            throw new Exception();
        }

        public Task<object> UncallableOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            return Task.FromResult<object>(null);
        }

        public Task<object> UncallableEntity([EntityTrigger] IDurableEntityContext ctx)
        {
            return Task.FromResult<object>(null);
        }

        public Task<object> UncallableActivity([ActivityTrigger] IDurableActivityContext ctx)
        {
            return Task.FromResult<object>(null);
        }
    }
}
