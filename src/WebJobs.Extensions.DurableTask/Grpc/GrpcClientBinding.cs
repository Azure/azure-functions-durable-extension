// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Grpc.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class GrpcClientBinding
    {
        internal static DurableClientAttribute GetAttribute(ServerCallContext context)
        {
            return new DurableClientAttribute
            {
                TaskHub = context.RequestHeaders.GetValue("Durable-TaskHub"),
                ConnectionName = context.RequestHeaders.GetValue("Durable-ConnectionName"),
            };
        }
    }
}
