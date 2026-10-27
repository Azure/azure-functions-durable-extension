// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configures worker-owned payload storage for .NET isolated Durable Functions.
/// </summary>
public static class FunctionsLargePayloadExtensions
{
    /// <summary>
    /// Registers a shared Azure Blob payload store and enables payload processing on
    /// bound clients, orchestrations and activities in this worker process.
    /// </summary>
    /// <param name="services">The worker services.</param>
    /// <param name="configure">The storage and payload-size configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDurableTaskExternalizedPayloads(
        this IServiceCollection services,
        Action<LargePayloadStorageOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.AddExternalizedPayloadStore(configure);
        return services.AddDurableTaskExternalizedPayloads();
    }

    /// <summary>
    /// Enables payload processing using an already registered <see cref="PayloadStore"/>
    /// and <see cref="LargePayloadStorageOptions"/>, including a custom payload store.
    /// </summary>
    /// <param name="services">The worker services.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDurableTaskExternalizedPayloads(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions<LargePayloadStorageOptions>();
        services.TryAddSingleton<IExternalizedPayloads, FunctionsExternalizedPayloads>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IFunctionMetadataTransformer, FunctionsPayloadMetadataTransformer>());
        return services;
    }
}
