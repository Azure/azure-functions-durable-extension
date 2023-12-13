// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#if NET6_0_OR_GREATER
using Grpc.Net.Client;
#endif

#if NETSTANDARD
using GrpcChannel = Grpc.Core.Channel;
#endif

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// The functions implementation of the durable task client provider.
/// </summary>
/// <remarks>
/// This class does NOT provide <see cref="FunctionsDurableTaskClient" /> is meant as a per-binding wrapper.
/// </remarks>
internal partial class FunctionsDurableClientProvider : IAsyncDisposable
{
    private readonly ReaderWriterLockSlim sync = new();
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly DurableTaskClientOptions options;
    private Dictionary<ClientKey, ClientHolder>? clients = new();

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FunctionsDurableClientProvider" /> class.
    /// </summary>
    /// <param name="loggerFactory">The service provider.</param>
    /// <param name="options">The client options.</param>
    public FunctionsDurableClientProvider(ILoggerFactory loggerFactory, IOptions<DurableTaskClientOptions> options)
    {
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        this.logger = loggerFactory.CreateLogger<FunctionsDurableClientProvider>();
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        try
        {
            this.sync.EnterWriteLock();
            try
            {
                if (this.disposed)
                {
                    return;
                }

                foreach (ClientHolder holder in this.clients!.Values)
                {
                    await holder.DisposeAsync();
                }

                this.clients = null;
                this.disposed = true;
            }
            finally
            {
                this.sync.ExitWriteLock();
            }
        }
        catch (ObjectDisposedException)
        {
            // this can happen when 'this.sync' is disposed from concurrent DisposeAsync() calls.
        }

        this.sync.Dispose();
    }

    /// <summary>
    /// Gets a <see cref="DurableTaskClient" /> by name and gRPC endpoint.
    /// </summary>
    /// <param name="endpoint">The gRPC endpoint this client should connect to.</param>
    /// <param name="taskHub">The name of the task hub this client is for.</param>
    /// <param name="connectionName">The name of the connection to use for the task-hub.</param>
    /// <returns>A <see cref="DurableTaskClient" />.</returns>
    public DurableTaskClient GetClient(Uri endpoint, string? taskHub, string? connectionName)
    {
        this.VerifyNotDisposed();
        this.sync.EnterReadLock();

        taskHub ??= string.Empty;
        connectionName ??= string.Empty;
        ClientKey key = new(endpoint, taskHub, connectionName);
        try
        {
            this.VerifyNotDisposed();
            if (this.clients!.TryGetValue(key, out ClientHolder? holder))
            {
                this.logger.LogTrace("DurableTaskClient resolved from cache");
                return holder.Client;
            }
        }
        finally
        {
            this.sync.ExitReadLock();
        }

        this.sync.EnterWriteLock();
        try
        {
            this.VerifyNotDisposed();
            if (this.clients!.TryGetValue(key, out ClientHolder? holder))
            {
                this.logger.LogTrace("DurableTaskClient resolved from cache");
                return holder.Client;
            }

            this.logger.LogTrace(
                "DurableTaskClient cache miss, constructing for Endpoint: '{Endpoint}', TaskHub: '{TaskHub}', ConnectionName: '{ConnectionName}'",
                endpoint,
                taskHub,
                connectionName);
            GrpcChannel channel = CreateChannel(key);
            GrpcDurableTaskClientOptions options = new()
            {
                Channel = channel,
                DataConverter = this.options.DataConverter,
                EnableEntitySupport = this.options.EnableEntitySupport,
            };

            ILogger logger = this.loggerFactory.CreateLogger<GrpcDurableTaskClient>();
            GrpcDurableTaskClient client = new(taskHub, options, logger);
            holder = new(client, channel);
            this.clients[key] = holder;
            return client;
        }
        finally
        {
            this.sync.ExitWriteLock();
        }
    }

    private void VerifyNotDisposed()
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(FunctionsDurableClientProvider));
        }
    }

    // Wrapper class to conveniently dispose/shutdown the client and channel together.
    private class ClientHolder : IAsyncDisposable
    {
        private readonly GrpcChannel channel;

        public ClientHolder(DurableTaskClient client, GrpcChannel channel)
        {
            this.Client = client;
            this.channel = channel;
        }

        public DurableTaskClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await this.Client.DisposeAsync();
                await this.channel.ShutdownAsync();
            }
            catch
            {
                // dispose should not throw and unsure how Channel multiple ShutdownAsync() calls behave.
            }
        }
    }

    private record ClientKey(Uri Address, string? Name, string? Connection)
    {
        private static readonly Dictionary<string, string> EmptyHeaders = new();

        public IReadOnlyDictionary<string, string> GetHeaders()
        {
            if (string.IsNullOrEmpty(this.Name) && string.IsNullOrEmpty(this.Connection))
            {
                return EmptyHeaders;
            }

            Dictionary<string, string> headers = new();
            if (!string.IsNullOrEmpty(this.Name))
            {
                headers["Durable-TaskHub"] = this.Name!;
            }

            if (!string.IsNullOrEmpty(this.Connection))
            {
                headers["Durable-ConnectionName"] = this.Connection!;
            }

            return headers;
        }
    }
}
