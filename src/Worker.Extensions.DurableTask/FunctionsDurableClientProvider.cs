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

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// The functions implementation of the durable task client provider.
/// </summary>
internal class FunctionsDurableClientProvider : IDurableTaskClientProvider, IAsyncDisposable
{
    private readonly ReaderWriterLockSlim sync = new();
    private readonly ILoggerFactory loggerFactory;
    private readonly DurableTaskClientOptions options;
    private Dictionary<string, DurableTaskClient>? clients = new();

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FunctionsDurableClientProvider" /> class.
    /// </summary>
    /// <param name="loggerFactory">The service provider.</param>
    /// <param name="options">The client options.</param>
    public FunctionsDurableClientProvider(ILoggerFactory loggerFactory, IOptions<DurableTaskClientOptions> options)
    {
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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

                foreach (DurableTaskClient client in this.clients!.Values)
                {
                    await client.DisposeAsync();
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

    public DurableTaskClient GetClient(string? grpcEndpointUri)
    {
        if (!Uri.TryCreate(grpcEndpointUri, UriKind.Absolute, out Uri uri))
        {
            throw new ArgumentException("Not a valid gRPC address.", nameof(grpcEndpointUri));
        }

        string address = $"{uri.Host}:{uri.Port}";
        this.VerifyNotDisposed();
        this.sync.EnterReadLock();
        try
        {
            this.VerifyNotDisposed();
            if (this.clients!.TryGetValue(address, out DurableTaskClient client))
            {
                return client;
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
            if (this.clients!.TryGetValue(address, out DurableTaskClient client))
            {
                return client;
            }

            GrpcDurableTaskClientOptions options = new()
            {
                Address = address,
                DataConverter = this.options.DataConverter,
            };

            ILogger logger = this.loggerFactory.CreateLogger<GrpcDurableTaskClient>();
            client = new GrpcDurableTaskClient(string.Empty, options, logger);
            this.clients[address] = client;
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
}
