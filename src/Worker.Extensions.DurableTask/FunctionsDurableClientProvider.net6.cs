// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Net.Http;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal partial class FunctionsDurableClientProvider
{
    // Default retry policy for transient gRPC failures.
    // This handles cases where the server is temporarily unavailable or rate limiting.
    private static readonly MethodConfig DefaultMethodConfig = new()
    {
        Names = { MethodName.Default },
        RetryPolicy = new RetryPolicy
        {
            MaxAttempts = 5,
            InitialBackoff = TimeSpan.FromSeconds(1),
            MaxBackoff = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 1.5,
            RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted },
        },
    };

    private static readonly ServiceConfig DefaultServiceConfig = new()
    {
        MethodConfigs = { DefaultMethodConfig },
    };

    private static GrpcChannel CreateChannel(ClientKey key, int? maxGrpcMessageSize, TimeSpan grpcHttpClientTimeout)
    {
        IReadOnlyDictionary<string, string> headers = key.GetHeaders();
        if (headers.Count == 0)
        {
            GrpcChannelOptions defaultOptions = new()
            {
                ServiceConfig = DefaultServiceConfig,
            };

            return GrpcChannel.ForAddress(key.Address, defaultOptions);
        }

        HttpClient httpClient = new()
        {
            Timeout = grpcHttpClientTimeout
        };

        foreach (KeyValuePair<string, string> header in headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        GrpcChannelOptions options = new()
        {
            HttpClient = httpClient,
            DisposeHttpClient = true,
            MaxReceiveMessageSize = maxGrpcMessageSize,
            ServiceConfig = DefaultServiceConfig,
        };

        return GrpcChannel.ForAddress(key.Address, options);
    }
}
#endif
