// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

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
        applicationBuilder.ConfigureDurableExtension();
    }
}
