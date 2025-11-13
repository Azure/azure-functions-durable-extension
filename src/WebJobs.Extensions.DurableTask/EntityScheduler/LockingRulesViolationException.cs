// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The exception that is thrown when application code violates the locking rules.
    /// </summary>
    public class LockingRulesViolationException : Exception
    {
        internal LockingRulesViolationException()
        {
        }

        internal LockingRulesViolationException(string message)
            : base(message)
        {
        }

        internal LockingRulesViolationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#pragma warning disable SYSLIB0051 // Type or member is obsolete
        internal LockingRulesViolationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#pragma warning restore SYSLIB0051
    }
}
