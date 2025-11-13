// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Exception result representing an operation that failed, in case
    /// the original exception is not serializable, or out-of-proc.
    /// </summary>
    [Serializable]
    public class OperationErrorException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OperationErrorException"/> class.
        /// </summary>
        public OperationErrorException()
        {
        }

        /// <summary>
        /// Initializes an new instance of the <see cref="OperationErrorException"/> class.
        /// </summary>
        /// <param name="errorMessage">The message that describes the error.</param>
        public OperationErrorException(string errorMessage)
            : base(errorMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationErrorException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
#pragma warning disable SYSLIB0051 // Type or member is obsolete
        protected OperationErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#pragma warning restore SYSLIB0051
    }
}