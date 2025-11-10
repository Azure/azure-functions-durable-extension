// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor(
    IFunctionExecutor inner,
    ExtendedSessionsCache extendedSessionsCache,
    IDurableTaskFactory factory,
    IOptions<DurableTaskWorkerOptions> options,
    IExceptionPropertiesProvider? exceptionPropertiesProvider = null)
    : IFunctionExecutor
{
    private DataConverter Converter => options.Value.DataConverter;

    public virtual ValueTask ExecuteAsync(FunctionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.TryGetOrchestrationBinding(out BindingMetadata? triggerBinding))
        {
            return this.RunOrchestrationAsync(context, triggerBinding);
        }

        if (context.TryGetEntityBinding(out triggerBinding))
        {
            // Entity functions are handled in middleware.
            return this.RunEntityAsync(context, triggerBinding);
        }

        if (context.TryGetActivityBinding(out triggerBinding))
        {
            // Activity functions are handled in middleware.
            return this.RunActivityAsync(context, triggerBinding);
        }

        throw new NotSupportedException(
            $"Function '{context.FunctionDefinition.Name}' is not supported by {nameof(DurableFunctionExecutor)}.");
    }
}
