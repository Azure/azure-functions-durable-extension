using System;
using Microsoft.DurableTask.Protobuf;
using Newtonsoft.Json;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;

internal class DurableSerializationException : Exception
{
    private Exception FromException;

    // We set the base class properties of this exception to the same as the parent, 
    // so that methods in the worker after this can still (typically) access the same information vs w/o
    // this exception type. 
    internal DurableSerializationException(Exception fromException) : base(fromException.Message, fromException.InnerException)
    {
        this.FromException = fromException;
    }

    public override string ToString()
    {
        TaskFailureDetails? failureDetails = ExceptionToTaskFailureDetailsRecursive(this.FromException);
        return JsonConvert.SerializeObject(failureDetails);
    }

    public override string? StackTrace => this.FromException.StackTrace;

    private static TaskFailureDetails? ExceptionToTaskFailureDetailsRecursive(Exception? fromException)
    {
        if (fromException is null)
        {
            return null;
        }
        return new TaskFailureDetails()
        {
            ErrorType = fromException.GetType().FullName,
            ErrorMessage = fromException.Message,
            StackTrace = fromException.StackTrace,
            InnerFailure = ExceptionToTaskFailureDetailsRecursive(fromException.InnerException),
            IsNonRetriable = false
        };
    }
}