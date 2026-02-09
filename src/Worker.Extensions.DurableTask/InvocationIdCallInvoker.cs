// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using Grpc.Core;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// A call invoker that adds the function invocation ID to gRPC metadata for correlation purposes.
/// </summary>
internal sealed class InvocationIdCallInvoker : CallInvoker
{
    private const string InvocationIdMetadataKey = "x-azure-functions-invocationid";

    private static readonly AsyncLocal<string?> CurrentInvocationId = new();

    private readonly CallInvoker inner;

    public InvocationIdCallInvoker(CallInvoker inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// Sets the current function invocation ID for the current async context.
    /// This will be propagated to gRPC calls made within this context.
    /// </summary>
    public static void SetCurrentInvocationId(string? invocationId)
    {
        CurrentInvocationId.Value = invocationId;
    }

    /// <summary>
    /// Gets the current function invocation ID from the async context.
    /// </summary>
    public static string? GetCurrentInvocationId()
    {
        return CurrentInvocationId.Value;
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options,
        TRequest request)
    {
        return this.inner.BlockingUnaryCall(method, host, AddInvocationIdToOptions(options), request);
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options,
        TRequest request)
    {
        return this.inner.AsyncUnaryCall(method, host, AddInvocationIdToOptions(options), request);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options,
        TRequest request)
    {
        return this.inner.AsyncServerStreamingCall(method, host, AddInvocationIdToOptions(options), request);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options)
    {
        return this.inner.AsyncClientStreamingCall<TRequest, TResponse>(method, host, AddInvocationIdToOptions(options));
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options)
    {
        return this.inner.AsyncDuplexStreamingCall<TRequest, TResponse>(method, host, AddInvocationIdToOptions(options));
    }

    private static CallOptions AddInvocationIdToOptions(CallOptions options)
    {
        string? invocationId = CurrentInvocationId.Value;
        if (string.IsNullOrEmpty(invocationId))
        {
            return options;
        }

        // Clone existing headers to avoid mutating caller-provided metadata
        // and filter out any existing invocation ID to prevent duplicates
        Metadata headers = new Metadata();
        if (options.Headers is not null)
        {
            foreach (var entry in options.Headers)
            {
                if (!string.Equals(entry.Key, InvocationIdMetadataKey, StringComparison.OrdinalIgnoreCase))
                {
                    headers.Add(entry);
                }
            }
        }

        headers.Add(InvocationIdMetadataKey, invocationId!);
        return options.WithHeaders(headers);
    }
}
