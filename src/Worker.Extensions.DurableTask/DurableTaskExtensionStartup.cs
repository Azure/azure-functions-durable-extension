﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Shims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        applicationBuilder.Services.AddSingleton<IDurableTaskClientProvider, FunctionsDurableClientProvider>();
        applicationBuilder.Services.AddOptions<DurableTaskClientOptions>()
            .PostConfigure<IServiceProvider>((opt, sp) =>
            {
                if (GetConverter(sp) is DataConverter converter)
                {
                    opt.DataConverter = converter;
                }
            });

        applicationBuilder.Services.AddOptions<DurableTaskWorkerOptions>()
            .PostConfigure<IServiceProvider>((opt, sp) =>
            {
                if (GetConverter(sp) is DataConverter converter)
                {
                    opt.DataConverter = converter;
                }
            });

        applicationBuilder.Services.TryAddSingleton(sp =>
        {
            DurableTaskWorkerOptions options = sp.GetRequiredService<IOptions<DurableTaskWorkerOptions>>().Value;
            ILoggerFactory factory = sp.GetRequiredService<ILoggerFactory>();
            return new DurableTaskShimFactory(options, factory); // For GrpcOrchestrationRunner
        });

        applicationBuilder.UseMiddleware<DurableTaskFunctionsMiddleware>();
    }

    private static DataConverter? GetConverter(IServiceProvider services)
    {
        WorkerOptions? worker = services.GetRequiredService<IOptions<WorkerOptions>>()?.Value;
        return worker?.Serializer is ObjectSerializer serializer ? new ObjectConverterShim(serializer) : null;
    }
}
