// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Worker;
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
    public static IFunctionsWorkerApplicationBuilder ConfigureDurableExtension(
        this IFunctionsWorkerApplicationBuilder builder)
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

        builder.Services.TryAddSingleton<DurableFunctionExecutor>();
        builder.Services.TryAddSingleton<ExtendedSessionsCache>();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IFunctionMetadataTransformer, DurableMetadataTransformer>());
        IDurableTaskWorkerBuilder workerBuilder = builder.Services.AddDurableTaskWorker().UseFunctions();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<DurableTaskWorkerOptions>>(
                new WorkerOptionsValidation(workerBuilder)));

        return builder;
    }

    /// <summary>
    /// Configures the Durable Task worker for the Functions Worker.
    /// </summary>
    /// <param name="builder">The Functions Worker application builder.</param>
    /// <returns>The Durable Task worker builder.</returns>
    public static IDurableTaskWorkerBuilder ConfigureDurableWorker(this IFunctionsWorkerApplicationBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.Services.AddDurableTaskWorker().UseFunctions();
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

    private class PostConfigureClientOptions(IOptionsMonitor<WorkerOptions> workerOptions)
        : IPostConfigureOptions<DurableTaskClientOptions>
    {
        public void PostConfigure(string? name, DurableTaskClientOptions options)
        {
            if (workerOptions.Get(name).Serializer is { } serializer)
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

    private class PostConfigureWorkerOptions(IOptionsMonitor<WorkerOptions> workerOptions)
        : IPostConfigureOptions<DurableTaskWorkerOptions>
    {
        public void PostConfigure(string? name, DurableTaskWorkerOptions options)
        {
            if (workerOptions.Get(name).Serializer is { } serializer)
            {
                options.DataConverter = new ObjectConverterShim(serializer);
            }
        }
    }

    private class WorkerOptionsValidation(IDurableTaskWorkerBuilder builder)
        : IValidateOptions<DurableTaskWorkerOptions>
    {
        public ValidateOptionsResult Validate(string? name, DurableTaskWorkerOptions options)
        {
            // Actually validating the builder, but using options resolution to make it happen.
            if (builder.Name == name && !builder.ValidateBuildTarget())
            {
                return ValidateOptionsResult.Fail(
                    $"Durable Task Worker build target {builder.BuildTarget} is invalid.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
