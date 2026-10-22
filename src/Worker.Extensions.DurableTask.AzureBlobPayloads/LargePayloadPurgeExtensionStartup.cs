// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: WorkerExtensionStartup(typeof(LargePayloadPurgeExtensionStartup))]

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>Registers startup validation for the optional payload purge Functions package.</summary>
public sealed class LargePayloadPurgeExtensionStartup : WorkerExtensionStartup
{
    /// <inheritdoc/>
    public override void Configure(IFunctionsWorkerApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<WorkerOptions>, PayloadStoreValidation>());
        applicationBuilder.Services.AddOptions<WorkerOptions>().ValidateOnStart();
    }

    private sealed class PayloadStoreValidation(
        IServiceScopeFactory scopeFactory, ILogger<PayloadStoreValidation> logger) : IValidateOptions<WorkerOptions>
    {
        private const string ConfigurationMessage =
            "The optional large payload purge Functions require a working PayloadStore registration. " +
            "Call ConfigureLargePayloadPurgeFunctions during worker configuration, or register a custom PayloadStore.";

        private const string AsyncDisposalMessage =
            "A scoped custom PayloadStore must support synchronous disposal. " +
            "Register an async-only custom store as a singleton so the host owns its asynchronous disposal.";

        public ValidateOptionsResult Validate(string? name, WorkerOptions options)
        {
            if (name != Options.DefaultName)
            {
                return ValidateOptionsResult.Skip;
            }

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                if (scope.ServiceProvider.GetService<PayloadStore>() is null)
                {
                    throw new InvalidOperationException("No PayloadStore is registered.");
                }
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("only implements IAsyncDisposable", StringComparison.Ordinal))
            {
                logger.LogError(exception, AsyncDisposalMessage);
                throw new InvalidOperationException(AsyncDisposalMessage, exception);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException or OptionsValidationException)
            {
                logger.LogError(exception, ConfigurationMessage);
                throw new InvalidOperationException(ConfigurationMessage, exception);
            }

            return ValidateOptionsResult.Success;
        }
    }
}
