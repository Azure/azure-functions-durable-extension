﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// ephemeral, typed view of the state of the actor, for use by applications in deserialized form.
    /// </summary>
    /// <typeparam name="T">the type of the state.</typeparam>
    internal class TypedStateView<T> : IStateView<T>, IStateView
    {
        private readonly DurableActorContext context;
        private readonly Formatting formatting;
        private readonly JsonSerializerSettings settings;

        private T state;

        private static JsonSerializerSettings defaultSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.None,
        };

        public TypedStateView(DurableActorContext context, Formatting formatting, JsonSerializerSettings settings)
        {
            this.context = context;
            this.formatting = formatting;
            this.settings = settings ?? defaultSettings;

            var actorState = context.State.ActorState;

            if (actorState == null)
            {
                this.state = default(T);
            }
            else
            {
                this.state = JsonConvert.DeserializeObject<T>(actorState, this.settings);
            }
        }

        public T Value
        {
            get
            {
                this.context.ThrowIfInvalidAccess();
                if (this.context.State?.CurrentStateView != this)
                {
                    throw new ObjectDisposedException(nameof(TypedStateView<T>), "The state view is no longer valid.");
                }

                return this.state;
            }

            set
            {
                this.context.ThrowIfInvalidAccess();
                if (this.context.State?.CurrentStateView != this)
                {
                    throw new ObjectDisposedException(nameof(TypedStateView<T>), "The state view is no longer valid.");
                }

                this.state = value;
            }
        }

        public void WriteBack()
        {
            this.context.State.ActorState = JsonConvert.SerializeObject(this.state, typeof(T), this.formatting, this.settings);
        }

        void IDisposable.Dispose()
        {
            if (this.context.State?.CurrentStateView == this)
            {
                this.WriteBack();
                this.context.State.CurrentStateView = null;
            }
        }
    }
}
