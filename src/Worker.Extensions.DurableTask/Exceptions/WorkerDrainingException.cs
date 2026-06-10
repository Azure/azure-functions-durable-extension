// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;

/// <summary>
/// Thrown by the worker when a Function completes while the worker is draining (shutting down).
/// </summary>
internal sealed class WorkerDrainingException : Exception
{
    /// <summary>
    /// Stable marker the host matches on to detect this exception after it crosses the gRPC boundary
    /// as a serialized string. The host keeps an identical copy of this literal; keep them in sync.
    /// </summary>
    internal const string Sentinel = "[DurableTask:WorkerDraining]";

    public WorkerDrainingException()
        : base($"The worker is shutting down and will not commit this result; the work item should be retried on another worker. {Sentinel}")
    {
    }
}
