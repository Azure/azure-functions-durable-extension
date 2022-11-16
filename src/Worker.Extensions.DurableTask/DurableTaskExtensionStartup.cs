// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: WorkerExtensionStartup(typeof(DurableTaskExtensionStartup))]

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// Startup class that registers the Durable Task middleware with the function app.
/// </summary>
public sealed class DurableTaskExtensionStartup : WorkerExtensionStartup
{
    /// <inheritdoc/>
    public override void Configure(IFunctionsWorkerApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.AddSingleton<FunctionsDurableClientProvider>();
        applicationBuilder.Services.AddOptions<DurableTaskClientOptions>()
            .PostConfigure<IServiceProvider>((opt, sp) =>
            {
                if (sp.GetService<DataConverter>() is DataConverter converter
                    && !ReferenceEquals(opt.DataConverter, JsonDataConverter.Default))
                {
                    opt.DataConverter = converter;
                }
            });

        applicationBuilder.UseMiddleware<DurableTaskFunctionsMiddleware>();
    }
}
