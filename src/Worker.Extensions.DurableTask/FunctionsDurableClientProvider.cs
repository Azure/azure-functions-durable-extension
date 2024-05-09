// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Functions.Worker.Extensions.Rpc;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// The functions implementation of the durable task client provider.
/// </summary>
/// <remarks>
/// This class does NOT provide <see cref="FunctionsDurableTaskClient" /> is meant as a per-binding wrapper.
/// </remarks>
internal partial class FunctionsDurableClientProvider
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly DurableTaskClientOptions options;
    private readonly FunctionsGrpcOptions grpcOptions;

    private readonly DurableTaskClient defaultClient;

    /// <summary>
    /// Initializes a new instance of <see cref="FunctionsDurableClientProvider" /> class.
    /// </summary>
    /// <param name="loggerFactory">The service provider.</param>
    /// <param name="options">The client options.</param>
    /// <param name="grpcOptions">The grpc options.</param>
    public FunctionsDurableClientProvider(
        ILoggerFactory loggerFactory,
        IOptions<DurableTaskClientOptions> options,
        IOptions<FunctionsGrpcOptions> grpcOptions)
    {
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        this.logger = loggerFactory.CreateLogger<FunctionsDurableClientProvider>();
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.grpcOptions = grpcOptions?.Value ?? throw new ArgumentNullException(nameof(grpcOptions));

        this.defaultClient = this.GetClientCore(null, null);
    }

    /// <summary>
    /// Gets a <see cref="DurableTaskClient" /> by name and gRPC endpoint.
    /// </summary>
    /// <param name="taskHub">The name of the task hub this client is for.</param>
    /// <param name="connectionName">The name of the connection to use for the task-hub.</param>
    /// <returns>A <see cref="DurableTaskClient" />.</returns>
    public DurableTaskClient GetClient(string? taskHub, string? connectionName)
    {
        if (string.IsNullOrEmpty(taskHub) && string.IsNullOrEmpty(connectionName))
        {
            // optimization for the most often used client.
            return this.defaultClient;
        }

        return this.GetClientCore(taskHub, connectionName);
    }

    private DurableTaskClient GetClientCore(string? name, string? connection)
    {
        GrpcDurableTaskClientOptions options = new()
        {
            CallInvoker = this.CreateCallInvoker(name, connection),
            DataConverter = this.options.DataConverter,
        };

        ILogger logger = this.loggerFactory.CreateLogger<GrpcDurableTaskClient>();
        return new GrpcDurableTaskClient(name ?? string.Empty, options, logger);
    }

    private CallInvoker CreateCallInvoker(string? name, string? connection)
    {
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(connection))
        {
            return this.grpcOptions.CallInvoker;
        }

        return this.grpcOptions.CallInvoker.Intercept(incoming =>
        {
            Metadata metadata = incoming;
            if (incoming.IsReadOnly)
            {
                metadata = new();
                foreach (Metadata.Entry entry in incoming)
                {
                    metadata.Add(entry);
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                metadata.Add("Durable-TaskHub", name);
            }

            if (!string.IsNullOrEmpty(connection))
            {
                metadata.Add("Durable-ConnectionName", connection);
            }

            return metadata;
        });
    }
}
