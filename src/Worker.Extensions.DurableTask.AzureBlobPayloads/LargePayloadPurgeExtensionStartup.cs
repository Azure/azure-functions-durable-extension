// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

[assembly: WorkerExtensionStartup(typeof(LargePayloadPurgeExtensionStartup))]

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>Registers startup validation for the optional payload purge Functions package.</summary>
public sealed class LargePayloadPurgeExtensionStartup : WorkerExtensionStartup
{
    /// <inheritdoc/>
    public override void Configure(IFunctionsWorkerApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, LargePayloadPurgeStartupValidator>());
    }
}
