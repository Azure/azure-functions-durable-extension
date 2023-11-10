// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;

namespace Microsoft.DurableTask;

/// <summary>
/// Extensions for <see cref="TaskOrchestrationContext"/>.
/// </summary>
public static class TaskOrchestrationContextExtensionMethods
{
    /// <summary>
    /// Makes an HTTP call using the information in the DurableHttpRequest.
    /// </summary>
    /// <param name="context">The task orchestration context.</param>
    /// <param name="request">The DurableHttpRequest used to make the HTTP call.</param>
    /// <returns>DurableHttpResponse</returns>
    public static async Task<DurableHttpResponse> CallHttpAsync(this TaskOrchestrationContext context, DurableHttpRequest request)
    {
        DurableHttpResponse response = await context.CallActivityAsync<DurableHttpResponse>(Constants.HttpTaskActivityReservedName, request);
        
        return response;
    }

    /// <summary>
    /// Makes an HTTP call to the specified uri.
    /// </summary>
    /// <param name="context">The task orchestration context.</param>
    /// <param name="method">HttpMethod used for api call.</param>
    /// <param name="uri">uri used to make the HTTP call.</param>
    /// <param name="content">Content passed in the HTTP request.</param>
    /// <param name="retryOptions">The retry option for the HTTP task.</param>
    /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
    public static async Task<DurableHttpResponse?> CallHttpAsync(this TaskOrchestrationContext context, HttpMethod method, Uri uri, string? content = null, HttpRetryOptions? retryOptions = null)
    {
        DurableHttpRequest request = new DurableHttpRequest(
            method: method,
            uri: uri,
            content: content,
            httpRetryOptions: retryOptions);

        DurableHttpResponse response = await context.CallActivityAsync<DurableHttpResponse>(Constants.HttpTaskActivityReservedName, request);

        return response;
    }
}
