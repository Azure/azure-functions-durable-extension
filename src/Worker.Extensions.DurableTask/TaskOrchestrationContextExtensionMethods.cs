// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Extensions for <see cref="TaskOrchestrationContext"/>.
/// </summary>
public static class TaskOrchestrationContextExtensionMethods
{
    /// <summary>
    /// Makes an HTTP call using the information in the DurableHttpRequest.
    /// </summary>
    /// <param name="context">The task orchestration context.</param>
    /// <param name="req">The DurableHttpRequest used to make the HTTP call.</param>
    /// <returns>DurableHttpResponse</returns>
    public static async Task<DurableHttpResponse> CallHttpAsync(this TaskOrchestrationContext context, DurableHttpRequest req)
    {
        string responseString = await context.CallActivityAsync<string>(Constants.HttpTaskActivityReservedName, req);
        
        DurableHttpResponse? response = JsonSerializer.Deserialize<DurableHttpResponse>(responseString);

        return response;
    }
}
