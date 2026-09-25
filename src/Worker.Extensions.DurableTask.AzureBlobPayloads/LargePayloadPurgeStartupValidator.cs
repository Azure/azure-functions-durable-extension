// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed class LargePayloadPurgeStartupValidator(
    IServiceScopeFactory scopeFactory, ILogger<LargePayloadPurgeStartupValidator> logger) : IHostedLifecycleService
{
    private const string ConfigurationMessage =
        "The optional large payload purge Functions require a working PayloadStore registration. " +
        "Call ConfigureLargePayloadPurgeFunctions during worker configuration, or register a custom PayloadStore.";

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        try
        {
            if (scope.ServiceProvider.GetService<PayloadStore>() is null)
            {
                throw new InvalidOperationException("No PayloadStore is registered.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OptionsValidationException)
        {
            logger.LogError(exception, ConfigurationMessage);
            throw new InvalidOperationException(ConfigurationMessage, exception);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
