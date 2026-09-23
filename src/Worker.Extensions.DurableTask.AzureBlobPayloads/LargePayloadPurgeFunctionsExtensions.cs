// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>Registration for SDK-owned external payload cleanup Functions.</summary>
public static class LargePayloadPurgeFunctionsExtensions
{
    /// <summary>
    /// Configures the payload store for the purge Functions supplied by the optional AzureBlobPayloads package.
    /// This does not enable cleanup or start an orchestration. Call the SDK client's explicit enable/disable API.
    /// </summary>
    /// <remarks>
    /// All cleanup activities run in the isolated worker. The configured store must refer to the same payload
    /// account and container as the provider for this worker's task hub. Activities use the default Durable
    /// client binding for that hub; customer client bindings retain their explicit hub and connection overrides.
    /// </remarks>
    /// <param name="builder">The isolated Functions worker builder.</param>
    /// <param name="configureStore">The shared SDK payload store configuration.</param>
    /// <returns>The original builder.</returns>
    public static IFunctionsWorkerApplicationBuilder ConfigureLargePayloadPurgeFunctions(
        this IFunctionsWorkerApplicationBuilder builder, Action<LargePayloadStorageOptions> configureStore)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configureStore is null)
        {
            throw new ArgumentNullException(nameof(configureStore));
        }

        builder.Services.AddExternalizedPayloadStore(configureStore);
        return builder;
    }
}
