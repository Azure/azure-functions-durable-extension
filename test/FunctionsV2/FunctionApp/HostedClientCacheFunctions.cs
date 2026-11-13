// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class HostedClientCacheFunctions
    {
        [NoAutomaticTrigger]
        public static void CaptureInternalClient(
            string hub,
            IDurableClient[] clients,
            [DurableClient(TaskHub = "{hub}")] IDurableClient client)
        {
            clients[0] = client;
        }

        [NoAutomaticTrigger]
        public static void CaptureExternalClient(
            string hub,
            IDurableClient[] clients,
            [DurableClient(TaskHub = "{hub}", ExternalClient = true)] IDurableClient client)
        {
            clients[0] = client;
        }
    }
}
