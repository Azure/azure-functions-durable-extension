// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: WorkerExtensionStartup(
    typeof(Microsoft.Azure.Functions.Worker.Extensions.DurableTask.LargePayloadPurgeExtensionStartup))]

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>Registers local readiness validation for enabling payload auto-purge.</summary>
public sealed class LargePayloadPurgeExtensionStartup : WorkerExtensionStartup
{
    /// <inheritdoc/>
    public override void Configure(IFunctionsWorkerApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.TryAddSingleton<
            ILargePayloadAutoPurgeReadinessValidator,
            PayloadStoreReadinessValidator>();
    }

    private sealed class PayloadStoreReadinessValidator(
        IServiceScopeFactory scopeFactory,
        ILogger<PayloadStoreReadinessValidator> logger)
        : ILargePayloadAutoPurgeReadinessValidator
    {
        private const string ConfigurationMessage =
            "The optional large payload purge Functions require a working local PayloadStore registration " +
            "before enabling large-payload auto-purge in this Functions process. " +
            "Call ConfigureLargePayloadPurgeFunctions during worker configuration, or register a custom PayloadStore.";

        public async Task ValidateAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                EnsurePayloadStore(scope.ServiceProvider);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException or OptionsValidationException)
            {
                logger.LogError(exception, ConfigurationMessage);
                throw new InvalidOperationException(ConfigurationMessage, exception);
            }
        }

        private static PayloadStore EnsurePayloadStore(IServiceProvider services)
            => services.GetService<PayloadStore>()
                ?? throw new InvalidOperationException("No PayloadStore is registered.");
    }
}
