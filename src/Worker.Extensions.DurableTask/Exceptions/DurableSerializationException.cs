using System;
using Google.Protobuf;
using Microsoft.DurableTask.Protobuf;
using Microsoft.DurableTask.Worker;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;

internal class DurableSerializationException : Exception
{
    private readonly Exception fromException;

    // We set the base class properties of this exception to the same as the parent, 
    // so that methods in the worker after this can still (typically) access the same information vs w/o
    // this exception type. 
    internal DurableSerializationException(Exception fromException) : 
        this(fromException, null)
    {
    }

    internal DurableSerializationException(Exception fromException, IExceptionPropertiesProvider? exceptionPropertiesProvider) 
        : base(CreateExceptionMessage(fromException, exceptionPropertiesProvider), fromException.InnerException)
    {
        this.fromException = fromException;
    }

    public override string ToString()
    {
        return this.Message;
    }

    private static string CreateExceptionMessage(Exception ex, IExceptionPropertiesProvider? exceptionPropertiesProvider)
    {
        TaskFailureDetails? failureDetails = TaskFailureDetailsConverter.TaskFailureFromException(ex, exceptionPropertiesProvider);
        return JsonFormatter.Default.Format(failureDetails);
    }

    public override string? StackTrace => this.fromException.StackTrace;
}