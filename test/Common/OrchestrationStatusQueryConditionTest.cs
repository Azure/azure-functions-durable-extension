// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DurableTask.Core;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OrchestrationStatusQueryConditionTest
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Parse_OrchestrationStatusQueryCondition()
        {
            var runtimeStatus = new List<OrchestrationRuntimeStatus>()
            {
                OrchestrationRuntimeStatus.Failed,
                OrchestrationRuntimeStatus.Terminated,
            };
            var createdTimeFrom = new DateTime(2019, 1, 3);
            var createdTimeTo = new DateTime(2019, 1, 4);
            var taskHubNames = new List<string>()
            {
                "baz",
                "qux",
            };

            var condition = new OrchestrationStatusQueryCondition
            {
                RuntimeStatus = runtimeStatus,
                CreatedTimeFrom = createdTimeFrom,
                CreatedTimeTo = createdTimeTo,
                TaskHubNames = taskHubNames,
            };

            var result = AzureStorageDurabilityProvider.ConvertWebjobsDurableConditionToAzureStorageCondition(condition);

            Assert.Equal(OrchestrationStatus.Failed, result.RuntimeStatus.First());
            Assert.Equal(OrchestrationStatus.Terminated, result.RuntimeStatus.Last());
            Assert.Equal(createdTimeFrom, result.CreatedTimeFrom);
            Assert.Equal(createdTimeTo, result.CreatedTimeTo);
            Assert.Equal(taskHubNames, result.TaskHubNames);
        }
    }
}
