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
    // Default service config JSON for retry policy.
    // This handles cases where the server is temporarily unavailable or rate limiting.
    // Using JSON format as required by Grpc.Core ChannelOption.
    private const string DefaultServiceConfigJson = @"{
        ""methodConfig"": [{
            ""name"": [{ ""service"": """" }],
            ""retryPolicy"": {
                ""maxAttempts"": 5,
                ""initialBackoff"": ""1s"",
                ""maxBackoff"": ""5s"",
                ""backoffMultiplier"": 1.5,
                ""retryableStatusCodes"": [""UNAVAILABLE"", ""RESOURCE_EXHAUSTED""]
            }
        }]
    }";

    private static Channel CreateChannel(ClientKey key, int? maxGrpcMessageSize, TimeSpan grpcHttpClientTimeout)
    {
        IReadOnlyDictionary<string, string> headers = key.GetHeaders();
        string address = $"{key.Address.Host}:{key.Address.Port}";
        var options = new List<ChannelOption>
        {
            new ChannelOption("grpc.service_config", DefaultServiceConfigJson),
        };

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
