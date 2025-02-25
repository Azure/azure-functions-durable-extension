// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
internal class TaskFailureDetailsConverter
{
    internal static P.TaskFailureDetails? TaskFailureFromException(Exception? fromException)
    {
        if (fromException is null)
        {
            return null;
        }
        return new P.TaskFailureDetails()
        {
            ErrorType = fromException.GetType().FullName,
            ErrorMessage = fromException.Message,
            StackTrace = fromException.StackTrace,
            InnerFailure = TaskFailureFromException(fromException.InnerException),
            IsNonRetriable = false
        };
    }
}
