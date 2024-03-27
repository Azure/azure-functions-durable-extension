// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace IsolatedEntities;

/// <summary>
/// Provides an http trigger to run functional tests for entities.
/// </summary>
public static class HttpTriggers
{
    [Function(nameof(RunAllTests))]
    public static async Task<HttpResponseData> RunAllTests(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tests/")] HttpRequestData request,
       [DurableClient] DurableTaskClient client,
       FunctionContext executionContext)
    {
        var context = new TestContext(client, executionContext);
        string result = await TestRunner.RunAsync(context, filter: null);
        HttpResponseData response = request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.WriteString(result);
        return response;
    }

    [Function(nameof(RunFilteredTests))]
    public static async Task<HttpResponseData> RunFilteredTests(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tests/{filter}")] HttpRequestData request,
      [DurableClient] DurableTaskClient client,
      FunctionContext executionContext,
      string filter)
    {
        var context = new TestContext(client, executionContext);
        string result = await TestRunner.RunAsync(context, filter);
        HttpResponseData response = request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.WriteString(result);
        return response;
    }

    [Function(nameof(ListAllTests))]
    public static async Task<HttpResponseData> ListAllTests(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tests/")] HttpRequestData request,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var context = new TestContext(client, executionContext);
        string result = await TestRunner.RunAsync(context, filter: null, listOnly: true);
        HttpResponseData response = request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.WriteString(result);
        return response;
    }

    [Function(nameof(ListFilteredTests))]
    public static async Task<HttpResponseData> ListFilteredTests(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tests/{filter}")] HttpRequestData request,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string filter)
    {
        var context = new TestContext(client, executionContext);
        string result = await TestRunner.RunAsync(context, filter, listOnly: true);
        HttpResponseData response = request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.WriteString(result);
        return response;
    }
}



