// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#if NET6_0_OR_GREATER
using System.Collections.Generic;
using System.Net.Http;
using Grpc.Net.Client;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal partial class FunctionsDurableClientProvider
{
    private static GrpcChannel CreateChannel(ClientKey key)
    {
        IReadOnlyDictionary<string, string> headers = key.GetHeaders();
        if (headers.Count == 0)
        {
            return GrpcChannel.ForAddress(key.Address);
        }


        HttpClient httpClient = new();
        foreach (KeyValuePair<string, string> header in headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        GrpcChannelOptions options = new()
        {
            HttpClient = httpClient,
            DisposeHttpClient = true,
        };

        return GrpcChannel.ForAddress(key.Address, options);
    }
}
#endif
