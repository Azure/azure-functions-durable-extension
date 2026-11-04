// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using Grpc.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc
{
    internal sealed class TaskHubRpcException : RpcException
    {
        public TaskHubRpcException(Status status, Exception cause)
            : base(status)
        {
            this.Cause = cause ?? throw new ArgumentNullException(nameof(cause));
        }

        public Exception Cause { get; }
    }
}
