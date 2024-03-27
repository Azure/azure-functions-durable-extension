// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace IsolatedEntities;

internal static class TestContextExtensions
{
    public static async Task<T> WaitForEntityStateAsync<T>(
        this TestContext context,
        EntityInstanceId entityInstanceId,
        TimeSpan? timeout = null,
        Func<T, string?>? describeWhatWeAreWaitingFor = null)
    {
        if (timeout == null)
        {
            timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
        }

        Stopwatch sw = Stopwatch.StartNew();

        EntityMetadata? response;

        do
        {
            response = await context.Client.Entities.GetEntityAsync(entityInstanceId, includeState: true);

            if (response != null)
            {
                if (describeWhatWeAreWaitingFor == null)
                {
                    break;
                }
                else
                {
                    var waitForResult = describeWhatWeAreWaitingFor(response.State.ReadAs<T>());

                    if (string.IsNullOrEmpty(waitForResult))
                    {
                        break;
                    }
                    else
                    {
                        context.Logger.LogInformation($"Waiting for {entityInstanceId} : {waitForResult}");
                    }
                }
            }
            else
            {
                context.Logger.LogInformation($"Waiting for {entityInstanceId} to have state.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
        while (sw.Elapsed < timeout);

        if (response != null)
        {
            string serializedState = response.State.Value;
            context.Logger.LogInformation($"Found state: {serializedState}");
            return response.State.ReadAs<T>();
        }
        else
        {
            throw new TimeoutException($"Durable entity '{entityInstanceId}' still doesn't have any state!");
        }
    }
}
