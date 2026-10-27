// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Net.Sockets;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class ExternalizedPayloadFixture : FunctionAppFixture
{
    public ExternalizedPayloadFixture(IMessageSink messageSink)
        : base(messageSink)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        this.HostAddress = new Uri($"http://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        this.functionAppProcess.DedicatedHostAddress = this.HostAddress;
        this.functionAppProcess.AdditionalEnvironmentVariables = new Dictionary<string, string>
        {
            ["DF_PAYLOAD_TESTS_ENABLED"] = "true",
            ["PAYLOAD_STORAGE_CONNECTION_STRING"] = "UseDevelopmentStorage=true",
            ["PAYLOAD_STORAGE_CONTAINER"] = $"payloads-{Guid.NewGuid():N}",
            ["AzureFunctionsJobHost__extensions__durableTask__storageProvider__payloadStorageEnabled"] = "false",
        };

        if (this.GetDurabilityProvider() != ConfiguredDurabilityProviderType.AzureManaged)
        {
            this.functionAppProcess.AdditionalEnvironmentVariables[
                "AzureFunctionsJobHost__extensions__durableTask__hubName"] = $"Payloads{Guid.NewGuid():N}";
        }
    }

    internal Uri HostAddress { get; }
}
