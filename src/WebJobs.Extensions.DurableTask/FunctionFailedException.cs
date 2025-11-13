// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The exception that is thrown when a sub-orchestrator or activity function fails
    /// with an error.
    /// </summary>
    /// <remarks>
    /// The `InnerException` property of this instance will contain additional information
    /// about the failed sub-orchestrator or activity function.
    /// </remarks>
    [Serializable]
    public class FunctionFailedException : Exception
    {
        internal FunctionFailedException()
        {
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="FunctionFailedException"/>.
        /// </summary>
        /// <param name="message">A message describing where to look for more details.</param>
        public FunctionFailedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="FunctionFailedException"/>.
        /// </summary>
        /// <param name="message">A message describing where to look for more details.</param>
        /// <param name="innerException">The exception that caused the function to fail.</param>
        public FunctionFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#pragma warning disable SYSLIB0051 // Type or member is obsolete
        internal FunctionFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#pragma warning restore SYSLIB0051
    }
}
