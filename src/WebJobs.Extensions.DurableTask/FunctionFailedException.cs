// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// The exception that is thrown when a sub-orchestrator or activity function fails
    /// with an error.
    /// </summary>
    public class FunctionFailedException : Exception
    {
        internal FunctionFailedException()
        {
        }

        internal FunctionFailedException(string message)
            : base(message)
        {
        }

        internal FunctionFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal FunctionFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
