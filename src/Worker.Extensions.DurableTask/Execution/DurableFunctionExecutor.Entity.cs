// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.DurableTask.Worker;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor
{
    private async ValueTask RunEntityAsync(FunctionContext context, BindingMetadata triggerBinding)
    {
        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string encodedEntityBatch)
        {
            throw new InvalidOperationException(
                "Entity batch was either missing from the input or not a string value.");
        }

        TaskEntityDispatcher dispatcher = new(encodedEntityBatch, context.InstanceServices);
        triggerInputData.Value = dispatcher;
        await inner.ExecuteAsync(context);
        context.GetInvocationResult().Value = dispatcher.Result;
    }
}
