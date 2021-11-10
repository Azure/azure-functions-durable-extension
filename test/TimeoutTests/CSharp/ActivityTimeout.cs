// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace TimeoutTests
{
    public static partial class TimeoutTests
    {
        [FunctionName(nameof(ActivityTimeout))]
        public static async Task<string> ActivityTimeout(
           [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            try
            {
                var result = await context.CallActivityAsync<string>("SlowActivity", null);
                return "Test failed: no exception thrown";
            }
            catch (Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionFailedException e)
                when (e.InnerException is Microsoft.Azure.WebJobs.Host.FunctionTimeoutException)
            {
                return "Test succeeded";
            }
            catch (Exception e)
            {
                return $"Test failed: wrong exception thrown: {e}";
            }
        }

        [FunctionName(nameof(SlowActivity))]
        public static string SlowActivity(
            [ActivityTrigger] IDurableActivityContext context,
            ILogger logger)
        {
            int seconds = 180;
            logger.LogWarning($"{context.InstanceId} starting slow activity duration={seconds}s");
            System.Threading.Thread.Sleep(seconds * 1000); // does not complete within the 00:02:00 timeout setting
            return $"Hello!";
        }
    }
}