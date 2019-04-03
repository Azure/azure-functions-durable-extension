﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
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

            var result = condition.Parse();

            Assert.Equal(OrchestrationStatus.Failed, result.RuntimeStatus.First());
            Assert.Equal(OrchestrationStatus.Terminated, result.RuntimeStatus.Last());
            Assert.Equal(createdTimeFrom, result.CreatedTimeFrom);
            Assert.Equal(createdTimeTo, result.CreatedTimeTo);
            Assert.Equal(taskHubNames, result.TaskHubNames);
        }
    }
}
