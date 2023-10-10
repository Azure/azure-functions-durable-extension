// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.DurableTask.Entities;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed partial class FunctionsOrchestrationContext
{
    private class EntityFeature : TaskOrchestrationEntityFeature
    {
        private readonly FunctionsOrchestrationContext parent;
        private readonly TaskOrchestrationEntityFeature inner;

        public EntityFeature(FunctionsOrchestrationContext parent, TaskOrchestrationEntityFeature inner)
        {
            this.parent = parent;
            this.inner = inner;
        }

        public override Task<TResult> CallEntityAsync<TResult>(
            EntityInstanceId id, string operationName, object? input = null, CallEntityOptions? options = null)
        {
            this.parent.EnsureLegalAccess();
            return this.inner.CallEntityAsync<TResult>(id, operationName, input, options);
        }

        public override Task CallEntityAsync(
            EntityInstanceId id, string operationName, object? input = null, CallEntityOptions? options = null)
        {
            this.parent.EnsureLegalAccess();
            return this.inner.CallEntityAsync(id, operationName, input, options);
        }

        public override Task SignalEntityAsync(
            EntityInstanceId id, string operationName, object? input = null, SignalEntityOptions? options = null)
        {
            this.parent.EnsureLegalAccess();
            return this.inner.SignalEntityAsync(id, operationName, input, options);
        }

        public override bool InCriticalSection([NotNullWhen(true)] out IReadOnlyList<EntityInstanceId>? entityIds)
        {
            this.parent.EnsureLegalAccess();
            return this.inner.InCriticalSection(out entityIds);
        }

        public override Task<IAsyncDisposable> LockEntitiesAsync(IEnumerable<EntityInstanceId> entityIds)
        {
            this.parent.EnsureLegalAccess();
            return this.inner.LockEntitiesAsync(entityIds);
        }
    }
}
