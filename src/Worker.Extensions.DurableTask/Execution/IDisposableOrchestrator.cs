// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal interface IDisposableOrchestrator : ITaskOrchestrator, IAsyncDisposable
{
}
