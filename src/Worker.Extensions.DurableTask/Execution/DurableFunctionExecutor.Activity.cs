// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;
using Microsoft.DurableTask.Worker;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor
{
    private async ValueTask RunActivityAsync(FunctionContext context, BindingMetadata triggerBinding)
    {
        try
        {
            await inner.ExecuteAsync(context);
            return;
        }
        catch (Exception ex)
        {
            IExceptionPropertiesProvider? exceptionPropertiesProvider = context.InstanceServices.GetService(typeof(IExceptionPropertiesProvider)) as IExceptionPropertiesProvider;
            throw new DurableSerializationException(ex, exceptionPropertiesProvider);
        }
    }
}
