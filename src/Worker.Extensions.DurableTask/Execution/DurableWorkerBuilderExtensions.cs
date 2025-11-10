// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal static class DurableWorkerBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="DurableMetadataTransformer"/> to the worker builder.
    /// </summary>
    /// <param name="builder">The worker builder.</param>
    /// <returns>The updated worker builder.</returns>
    public static IDurableTaskWorkerBuilder UseFunctions(this IDurableTaskWorkerBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.UseBuildTarget<Worker>();

        // A very workaround way to get IDurableTaskFactory directly into the DI container.
        builder.Services.TryAddSingleton(sp =>
        {
            var worker = (Worker)builder.Build(sp);
            return worker.Factory;
        });

        return builder;
    }

    /// <summary>
    /// Checks if the build target is set to what functions expects.
    /// </summary>
    /// <param name="builder">The builder to validate.</param>
    /// <returns><c>true</c> if valid, <c>false</c> otherwise.</returns>
    public static bool ValidateBuildTarget(this IDurableTaskWorkerBuilder builder)
    {
        return builder.BuildTarget == typeof(Worker);
    }

#pragma warning disable CS9113 // Parameter is unread. Suppressed to let a breaking change get fixed before we remove this parameter.
    private class Worker(string name, IDurableTaskFactory factory, IExceptionPropertiesProvider? provider = null) : DurableTaskWorker(name, factory)
    {
        public new IDurableTaskFactory Factory => base.Factory;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
#pragma warning restore CS9113 // Parameter is unread.
}