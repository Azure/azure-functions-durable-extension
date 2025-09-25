using System;
using Microsoft.DurableTask;

internal interface IDisposableOrchestrator : ITaskOrchestrator, IAsyncDisposable
{
}
