// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#if NETSTANDARD
using System;
using System.Collections.Generic;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal partial class FunctionsDurableClientProvider
{
    private static Channel CreateChannel(ClientKey key, int? maxGrpcMessageSize, TimeSpan grpcHttpClientTimeout)
    {
        IReadOnlyDictionary<string, string> headers = key.GetHeaders();
        string address = $"{key.Address.Host}:{key.Address.Port}";
        var options = new List<ChannelOption>();

        if (maxGrpcMessageSize.HasValue)
        {
            options.Add(new ChannelOption(ChannelOptions.MaxReceiveMessageLength, maxGrpcMessageSize.Value));
        }

        return headers.Count > 0
            ? new ChannelWithHeaders(address, headers, options)
            : new Channel(address, ChannelCredentials.Insecure, options);
    }

    private class ChannelWithHeaders : Channel
    {
        private readonly IReadOnlyDictionary<string, string> headers;

        public ChannelWithHeaders(string address, IReadOnlyDictionary<string, string> headers, IEnumerable<ChannelOption> options)
            : base(address, ChannelCredentials.Insecure, options)
        {
            this.headers = headers;
        }

        public override CallInvoker CreateCallInvoker()
        {
            return base.CreateCallInvoker().Intercept(metadata =>
            {
                foreach (KeyValuePair<string, string> kvp in this.headers)
                {
                    metadata.Add(kvp.Key, kvp.Value);
                }

                return metadata;
            });
        }
    }
}
#endif
