// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Exception used to describe various issues encountered by the entity scheduler.
    /// </summary>
    [Serializable]
    public class EntitySchedulerException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntitySchedulerException"/> class.
        /// </summary>
        public EntitySchedulerException()
        {
        }

        /// <summary>
        /// Initializes an new instance of the <see cref="EntitySchedulerException"/> class.
        /// </summary>
        /// <param name="errorMessage">The message that describes the error.</param>
        /// <param name="innerException">The exception that was caught.</param>
        public EntitySchedulerException(string errorMessage, Exception innerException)
            : base(errorMessage, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntitySchedulerException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
#pragma warning disable SYSLIB0051 // Type or member is obsolete
        protected EntitySchedulerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#pragma warning restore SYSLIB0051
    }
}