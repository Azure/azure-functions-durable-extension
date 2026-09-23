// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.ConfigureLargePayloadPurgeFunctions(options =>
        {
            options.ConnectionString = Environment.GetEnvironmentVariable("PurgePayloadConnection")
                ?? throw new InvalidOperationException("PurgePayloadConnection is required.");
            options.ContainerName = Environment.GetEnvironmentVariable("PurgePayloadContainer")
                ?? throw new InvalidOperationException("PurgePayloadContainer is required.");
        });
        worker.UseMiddleware<WorkerEvidenceMiddleware>();
    })
    .Build();

await host.RunAsync();

internal sealed class WorkerEvidenceMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        context.GetLogger(nameof(WorkerEvidenceMiddleware)).LogInformation(
            "Isolated purge E2E invocation: WorkerPid={WorkerPid}, Function={Function}, EntryPoint={EntryPoint}, InvocationId={InvocationId}",
            Environment.ProcessId, context.FunctionDefinition.Name, context.FunctionDefinition.EntryPoint, context.InvocationId);
        await next(context);
    }
}
