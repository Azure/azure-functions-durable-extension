// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core.Interceptors;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed class FunctionsExternalizedPayloads : IExternalizedPayloads
{
    private readonly FunctionsPayloadStore store;
    private readonly LargePayloadStorageOptions options;

    public FunctionsExternalizedPayloads(PayloadStore store, IOptions<LargePayloadStorageOptions> options)
    {
        this.store = new FunctionsPayloadStore(store);
        this.options = options.Value;
        this.ClientInterceptor = new FunctionsPayloadInterceptor(this, this.store, this.options);
    }

    public Interceptor ClientInterceptor { get; }

    public void ValidateSize(string? payload)
    {
        if (payload is { Length: > 0 } && !this.store.IsKnownPayloadToken(payload))
        {
            int bytes = Encoding.UTF8.GetByteCount(payload);
            if (bytes >= this.options.ThresholdBytes && bytes > this.options.MaxPayloadBytes)
            {
                throw new PayloadStorageException(
                    $"Payload size {bytes} bytes exceeds the configured maximum of {this.options.MaxPayloadBytes} bytes.");
            }
        }
    }

    public async Task<string?> ExternalizeAsync(string? payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (payload is null || payload.Length == 0)
        {
            return payload;
        }

        if (this.store.IsKnownPayloadToken(payload))
        {
            return this.store.QuoteToken(payload);
        }

        this.ValidateSize(payload);
        return Encoding.UTF8.GetByteCount(payload) < this.options.ThresholdBytes
            ? payload
            : await this.store.UploadAsync(payload, cancellationToken);
    }

    public async Task<string?> ResolveAsync(string? payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return payload is { Length: > 0 } && this.store.IsKnownPayloadToken(payload)
            ? await this.store.DownloadAsync(payload, cancellationToken)
            : payload;
    }

    private sealed class FunctionsPayloadInterceptor(
        IExternalizedPayloads payloads,
        PayloadStore store,
        LargePayloadStorageOptions options) : PayloadInterceptor<object, object>(store, options)
    {
        protected override Task ExternalizeRequestPayloadsAsync<TRequest>(TRequest request, CancellationToken cancellation)
            => request is IMessage message
                ? ExternalizedPayloads.TransformAsync(message, payloads, externalize: true, cancellation)
                : Task.CompletedTask;

        protected override Task ResolveResponsePayloadsAsync<TResponse>(TResponse response, CancellationToken cancellation)
            => response is IMessage message
                ? ExternalizedPayloads.TransformAsync(message, payloads, externalize: false, cancellation)
                : Task.CompletedTask;
    }

    private sealed class FunctionsPayloadStore(PayloadStore inner) : PayloadStore
    {
        public override async Task<string> UploadAsync(string payLoad, CancellationToken cancellationToken)
        {
            string token = await inner.UploadAsync(payLoad, cancellationToken);
            if (!inner.IsKnownPayloadToken(token))
            {
                throw new PayloadStorageException("The configured payload store returned an unrecognized reference.");
            }

            return JsonSerializer.Serialize(token);
        }

        public override Task<string> DownloadAsync(string token, CancellationToken cancellationToken)
            => inner.DownloadAsync(this.Unwrap(token), cancellationToken);

        public override bool IsKnownPayloadToken(string value)
            => inner.IsKnownPayloadToken(this.Unwrap(value));

        public string QuoteToken(string token) => JsonSerializer.Serialize(this.Unwrap(token));

        private string Unwrap(string value)
        {
            if (inner.IsKnownPayloadToken(value) || !value.StartsWith("\"", StringComparison.Ordinal))
            {
                return value;
            }

            try
            {
                return JsonSerializer.Deserialize<string>(value) ?? value;
            }
            catch (JsonException)
            {
                // Token recognition must leave ordinary, non-JSON strings untouched.
                return value;
            }
        }
    }
}
