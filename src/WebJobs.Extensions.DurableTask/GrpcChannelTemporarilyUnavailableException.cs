// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Exception thrown when the local gRPC channel used for communication between the
    /// Durable Task extension host and the out-of-process worker is temporarily unavailable.
    /// </summary>
    /// <remarks>
    /// This is a transient, platform-level error: the gRPC sidecar may still be starting or
    /// may have stopped unexpectedly. The extension treats this as retriable so that
    /// orchestrations and activities are safely aborted and retried by the durable backend
    /// rather than being marked as permanently failed.
    /// </remarks>
    [Serializable]
    public class GrpcChannelTemporarilyUnavailableException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcChannelTemporarilyUnavailableException"/> class.
        /// </summary>
        public GrpcChannelTemporarilyUnavailableException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcChannelTemporarilyUnavailableException"/> class.
        /// </summary>
        /// <param name="message">A message describing the unavailability condition.</param>
        public GrpcChannelTemporarilyUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcChannelTemporarilyUnavailableException"/> class.
        /// </summary>
        /// <param name="message">A message describing the unavailability condition.</param>
        /// <param name="innerException">The inner exception that caused this error.</param>
        public GrpcChannelTemporarilyUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#pragma warning disable SYSLIB0051 // Type or member is obsolete
        protected GrpcChannelTemporarilyUnavailableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#pragma warning restore SYSLIB0051
    }
}
