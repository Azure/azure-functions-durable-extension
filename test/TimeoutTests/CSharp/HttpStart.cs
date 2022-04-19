// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace TimeoutTests
{
    public static class HttpStart
    {
        [FunctionName("StartTest")]
        public static async Task<IActionResult> StartTest(
            [HttpTrigger(AuthorizationLevel.Function, methods: "get", Route = "start/{testname}")] HttpRequest req,
            string testname,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            try
            {
                string instanceId = await starter.StartNewAsync(testname, null);
                log.LogInformation($"Started {testname} with ID = '{instanceId}'.");
                return starter.CreateCheckStatusResponse(req, instanceId);
            }
            catch(Exception exception)
            {
                return new OkObjectResult(new { message = "could not start test", testname, exception = exception.ToString() });
            }
        }

        [FunctionName("StartAll")]
        public static async Task<IActionResult> StartAllTests(
            [HttpTrigger(AuthorizationLevel.Function, methods: "get", Route = "start")] HttpRequest req,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            try
            {
                async Task Start(string testname)
                {
                    string instanceId = await starter.StartNewAsync(testname, null);
                    log.LogInformation($"Started {testname} with ID = '{instanceId}'.");
                }

                await Task.WhenAll(
                    Start("ActivityTimeout"),
                    //Start("OrchestrationTimeout"),
                    Start("EntityTimeout1"),
                    Start("EntityTimeout2"),
                    Start("EntityBatch1"),
                    Start("EntityBatch2")
                    );

                return new OkObjectResult(new {message = "started all tests"});
            }
            catch (Exception exception)
            {
                return new OkObjectResult(new { message = "could not start all tests", exception = exception.ToString() });
            }
        }
    }
}
