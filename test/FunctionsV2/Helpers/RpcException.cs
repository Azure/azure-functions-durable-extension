// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Script.Workers.Rpc
{
    // The extension does not reference the Script host. Match its public exception contract
    // so tests exercise the host-specific message extraction without that dependency.
    internal class RpcException : Exception
    {
        public RpcException(string result, string message, string stack)
            : base($"Result: {result}\nException: {message}\nStack: {stack}")
        {
            this.RemoteMessage = message;
        }

        public string RemoteMessage { get; }
    }
}
