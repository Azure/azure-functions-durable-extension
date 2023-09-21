﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Worker.Grpc;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Represents a task entity dispatch invocation.
/// </summary>
/// <remarks>
/// This type is used to aid in dispatching a <see cref="EntityTriggerAttribute"/> to the operation receiver object.
/// </remarks>
public sealed class TaskEntityDispatcher
{
    private readonly string request;
    private readonly IServiceProvider services;

    internal TaskEntityDispatcher(string request, IServiceProvider services)
    {
        this.request = request;
        this.services = services;
    }

    internal string Result { get; private set; } = string.Empty;

    /// <summary>
    /// Dispatches this entity trigger to the provided <see cref="ITaskEntity"/>.
    /// </summary>
    /// <param name="entity">The task entity to dispatch to.</param>
    /// <returns>A task that completes when the dispatch has finished.</returns>
    public async Task DispatchAsync(ITaskEntity entity)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        this.Result = await GrpcEntityRunner.LoadAndRunAsync(this.request, entity);
    }

    /// <summary>
    /// <para>Dispatches the entity trigger to an instance of the provided <typeparamref name="T"/>.</para>
    /// <para>
    /// If <typeparamref name="T"/> is a <see cref="ITaskEntity"/>, it will be activated from
    /// <see cref="IServiceProvider"/> and then be dispatched to.
    /// </para>
    /// <para>
    /// If <typeparamref name="T"/> is not <see cref="ITaskEntity"/>, it is assumed the <typeparamref name="T"/>
    /// represents the entity state and it will be deserialized and dispatched directly to the state.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type to dispatch to.</typeparam>
    /// <returns>A task that completes when the dispatch has finished.</returns>
    public Task DispatchAsync<T>()
    {
        if (typeof(ITaskEntity).IsAssignableFrom(typeof(T)))
        {
            ITaskEntity entity = (ITaskEntity)ActivatorUtilities.GetServiceOrCreateInstance<T>(this.services);
            return this.DispatchAsync(entity);
        }

        return this.DispatchAsync(new StateEntity<T>());
    }

    private class StateEntity<T> : TaskEntity<T>
    {
        protected override bool AllowStateDispatch => true;
    }
}
