// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Worker;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal sealed class TypeHintingDurableTaskFactory : IDurableTaskFactory2
{
    private readonly IDurableTaskFactory inner;
    private readonly IDurableTaskFactory2? inner2;

    public TypeHintingDurableTaskFactory(IDurableTaskFactory inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.inner2 = inner as IDurableTaskFactory2;
    }

    public bool TryCreateActivity(TaskName name, IServiceProvider services, [NotNullWhen(true)] out ITaskActivity? activity)
    {
        if (!this.inner.TryCreateActivity(name, services, out activity))
        {
            return false;
        }

        activity = SerializationHintTaskActivity.Wrap(activity);
        return true;
    }

    public bool TryCreateOrchestrator(
        TaskName name,
        IServiceProvider services,
        [NotNullWhen(true)] out ITaskOrchestrator? orchestrator)
    {
        return this.inner.TryCreateOrchestrator(name, services, out orchestrator);
    }

    public bool TryCreateEntity(TaskName name, IServiceProvider services, [NotNullWhen(true)] out ITaskEntity? entity)
    {
        if (this.inner2 is null)
        {
            entity = null;
            return false;
        }

        return this.inner2.TryCreateEntity(name, services, out entity);
    }

    internal static IDurableTaskFactory WrapIfNeeded(IDurableTaskFactory factory)
    {
        if (factory is TypeHintingDurableTaskFactory)
        {
            return factory;
        }

        return new TypeHintingDurableTaskFactory(factory);
    }

    internal sealed class SerializationHintTaskActivity : ITaskActivity
    {
        private readonly ITaskActivity inner;

        private SerializationHintTaskActivity(ITaskActivity inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Type InputType => this.inner.InputType;

        public Type OutputType => this.inner.OutputType;

        public Task<object?> RunAsync(TaskActivityContext context, object? input)
        {
            return this.RunWithHintAsync(context, input);
        }

        internal static ITaskActivity Wrap(ITaskActivity activity)
        {
            if (activity is SerializationHintTaskActivity)
            {
                return activity;
            }

            return new SerializationHintTaskActivity(activity);
        }

        private async Task<object?> RunWithHintAsync(TaskActivityContext context, object? input)
        {
            object? result = await this.inner.RunAsync(context, input).ConfigureAwait(false);
            ObjectConverterShim.SetSerializationType(this.OutputType);
            return result;
        }
    }
}
