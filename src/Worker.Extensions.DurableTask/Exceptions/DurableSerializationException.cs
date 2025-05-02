using System;
using Microsoft.DurableTask.Protobuf;
using Newtonsoft.Json;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;

internal class DurableSerializationException : Exception
{
    private readonly Exception fromException;

    // We set the base class properties of this exception to the same as the parent, 
    // so that methods in the worker after this can still (typically) access the same information vs w/o
    // this exception type. 
    internal DurableSerializationException(Exception fromException) : base(CreateExceptionMessage(fromException), fromException.InnerException)
    {
        this.fromException = fromException;
    }

    public override string ToString()
    {
        return this.Message;
    }

    // Serilize FailureDetails to JSON
    private static string CreateExceptionMessage(Exception ex)
    {
        TaskFailureDetails? failureDetails = TaskFailureDetailsConverter.TaskFailureFromException(ex);
        return JsonConvert.SerializeObject(failureDetails);
    }

    public override string? StackTrace => this.fromException.StackTrace;
}