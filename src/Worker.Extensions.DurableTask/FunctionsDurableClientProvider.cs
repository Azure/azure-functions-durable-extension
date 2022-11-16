// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// The functions implementation of the durable task client provider.
/// </summary>
internal class FunctionsDurableClientProvider : IAsyncDisposable
{
    private readonly ReaderWriterLockSlim sync = new();
    private readonly ILoggerFactory loggerFactory;
    private readonly DurableTaskClientOptions options;
    private Dictionary<(string Name, string Address), ClientHolder>? clients = new();

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
    /// <param name="taskHubName">
    /// The name of the task hub this client is for. Can be <see cref="string.Empty" /> but not <c>null</c>.
    /// </param>
    /// <param name="grpcEndpointUri">The gRPC endpoint this client should connect to.</param>
    /// <returns>A <see cref="DurableTaskClient" />.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="taskHubName" /> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="grpcEndpointUri" /> is an invalid URI.</exception>
    public DurableTaskClient GetClient(string taskHubName, string grpcEndpointUri)
    {
        if (taskHubName is null)
        {
            throw new ArgumentNullException(nameof(taskHubName));
        }

        if (!Uri.TryCreate(grpcEndpointUri, UriKind.Absolute, out Uri uri))
        {
            throw new ArgumentException("Not a valid gRPC address.", nameof(grpcEndpointUri));
        }

        string address = $"{uri.Host}:{uri.Port}";
        this.VerifyNotDisposed();
        this.sync.EnterReadLock();
        (string taskHubName, string address) key = (taskHubName, address);
        try
        {
            this.VerifyNotDisposed();
            if (this.clients!.TryGetValue(key, out ClientHolder holder))
            {
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
            if (this.clients!.TryGetValue(key, out ClientHolder holder))
            {
                return holder.Client;
            }

            Channel channel = new NamedClientChannel(taskHubName, address);
            GrpcDurableTaskClientOptions options = new()
            {
                Channel = channel,
                DataConverter = this.options.DataConverter,
            };

            ILogger logger = this.loggerFactory.CreateLogger<GrpcDurableTaskClient>();
            GrpcDurableTaskClient client = new(string.Empty, options, logger);
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

    // Custom channel to append our TaskHubName as a header.
    private class NamedClientChannel : Channel
    {
        private readonly string name;

        public NamedClientChannel(string name, string address)
            : base(address, ChannelCredentials.Insecure)
        {
            this.name = name;
        }

        public override CallInvoker CreateCallInvoker()
        {
            return new NamedClientCallInvoker(this.name, this);
        }
    }

    // Custom call invoker to append our TaskHubName as a header. Instantiated from NamedClientChannel.
    private class NamedClientCallInvoker : DefaultCallInvoker
    {
        private readonly string name;

        public NamedClientCallInvoker(string name, Channel channel)
            : base(channel)
        {
            this.name = name;
        }

        protected override CallInvocationDetails<TRequest, TResponse> CreateCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options)
        {
            // Is there a better way to add a header to all calls from a channel?
            options = options.WithHeaders(this.AddHeader(options.Headers));
            return base.CreateCall(method, host, options);
        }

        private Metadata AddHeader(Metadata headers)
        {
            Metadata newHeaders = new();
            if (headers is { Count: > 0 })
            {
                foreach (Metadata.Entry entry in headers)
                {
                    newHeaders.Add(entry);
                }
            }

            newHeaders.Add("TaskHubName", this.name);
            return newHeaders;
        }
    }

    // Wrapper class to conveniently dispose/shutdown the client and channel together.
    private class ClientHolder : IAsyncDisposable
    {
        private readonly Channel channel;

        public ClientHolder(DurableTaskClient client, Channel channel)
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
                // dispose should not through and unsure how Channel multiple ShutdownAsync() calls behave.
            }
        }
    }
}
