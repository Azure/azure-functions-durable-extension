// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Grpc;
using Microsoft.DurableTask.Worker.Shims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Extensions for <see cref="IFunctionsWorkerApplicationBuilder"/>.
/// </summary>
public static class FunctionsWorkerApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the Durable Functions extension for the worker.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The <paramref name="builder"/> for call chaining.</returns>
    public static IFunctionsWorkerApplicationBuilder ConfigureDurableExtension(this IFunctionsWorkerApplicationBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.TryAddSingleton<FunctionsDurableClientProvider>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<DurableTaskClientOptions>, ConfigureClientOptions>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<DurableTaskClientOptions>, PostConfigureClientOptions>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<DurableTaskWorkerOptions>, ConfigureWorkerOptions>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<DurableTaskWorkerOptions>, PostConfigureWorkerOptions>());

        builder.Services.TryAddSingleton(sp =>
        {
            DurableTaskWorkerOptions options = sp.GetRequiredService<IOptions<DurableTaskWorkerOptions>>().Value;
            ILoggerFactory factory = sp.GetRequiredService<ILoggerFactory>();
            return new DurableTaskShimFactory(options, factory); // For GrpcOrchestrationRunner
        });

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<WorkerOptions>, ConfigureInputConverter>());
        if (!builder.Services.Any(d => d.ServiceType == typeof(DurableTaskFunctionsMiddleware)))
        {
            builder.UseMiddleware<DurableTaskFunctionsMiddleware>();
        }
        builder.Services.TryAddSingleton(new ExtendedSessionsCache());

        return builder;
    }

    private class ConfigureInputConverter : IConfigureOptions<WorkerOptions>
    {
        public void Configure(WorkerOptions options)
        {
            options.InputConverters.Register<OrchestrationInputConverter>();
        }
    }

    private class ConfigureClientOptions : IConfigureOptions<DurableTaskClientOptions>
    {
        public void Configure(DurableTaskClientOptions options)
        {
            options.EnableEntitySupport = true;
        }
    }

    private class PostConfigureClientOptions : IPostConfigureOptions<DurableTaskClientOptions>
    {
        readonly IOptionsMonitor<WorkerOptions> workerOptions;

        public PostConfigureClientOptions(IOptionsMonitor<WorkerOptions> workerOptions)
        {
            this.workerOptions = workerOptions;
        }

        public void PostConfigure(string name, DurableTaskClientOptions options)
        {
            if (this.workerOptions.Get(name).Serializer is { } serializer)
            {
                options.DataConverter = new ObjectConverterShim(serializer);
            }
        }
    }

    private class ConfigureWorkerOptions : IConfigureOptions<DurableTaskWorkerOptions>
    {
        public void Configure(DurableTaskWorkerOptions options)
        {
            options.EnableEntitySupport = true;
        }
    }

    private class PostConfigureWorkerOptions : IPostConfigureOptions<DurableTaskWorkerOptions>
    {
        readonly IOptionsMonitor<WorkerOptions> workerOptions;

        public PostConfigureWorkerOptions(IOptionsMonitor<WorkerOptions> workerOptions)
        {
            this.workerOptions = workerOptions;
        }

        public void PostConfigure(string name, DurableTaskWorkerOptions options)
        {
            if (this.workerOptions.Get(name).Serializer is { } serializer)
            {
                options.DataConverter = new ObjectConverterShim(serializer);
            }
        }
    }
}
