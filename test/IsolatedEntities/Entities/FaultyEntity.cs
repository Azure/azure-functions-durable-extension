// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DurableTask.Core.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace IsolatedEntities
{
    // we use a low-level ITaskEntity so we can intercept some of the operations without going through
    // the default sequence of serialization and deserialization of state. This is needed to construct
    // this type of test, it does not reflect typical useage. 
    public class FaultyEntity : ITaskEntity
    {
        class State
        {
            [JsonInclude]
            public int Value { get; set; }

            [JsonInclude]
            [JsonConverter(typeof(CustomSerialization.ProblematicObjectJsonConverter))]
            public CustomSerialization.ProblematicObject? ProblematicObject { get; set; }

            [JsonInclude]
            public int NumberIncrementsSent { get; set; }

            public Task Send(EntityInstanceId target, TaskEntityContext context)
            {
                var desc = $"{++this.NumberIncrementsSent}:{this.Value}";
                context.SignalEntity(target, desc);
                return Task.CompletedTask;
            }
        }
        
        [Function(nameof(FaultyEntity))]
        public async Task EntryPoint([EntityTrigger] TaskEntityDispatcher dispatcher)
        {
            await dispatcher.DispatchAsync<FaultyEntity>();
        }

        public static void ThrowTestException()
        {
            throw new TestException("KABOOM");
        }

        [Serializable]
        public class TestException : Exception
        {
            public TestException() : base() { }
            public TestException(string message) : base(message) { }
            public TestException(string message, Exception inner) : base(message, inner) { }
        }

        public async ValueTask<object?> RunAsync(TaskEntityOperation operation)
        {
            State? Get()
            {
                return (State?)operation.State.GetState(typeof(State));
            }
            State GetOrCreate()
            {
                State? s = Get();
                if (s is null)
                {
                    operation.State.SetState(s = new State());
                }
                return s;
            }

            switch (operation.Name)
            {
                case "Exists":
                    {
                        try
                        {
                            return Get() != null;
                        }
                        catch (Exception) // the entity has state, even if that state is corrupted
                        {
                            return true;
                        }
                    }
               case "Delay":
                    {
                        int delayInSeconds = (int)operation.GetInput(typeof(int))!;
                        await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));
                        return default;
                    }
                case "Delete":
                    {
                        operation.State.SetState(null);
                        return default;
                    }
                case "DeleteWithoutReading":
                    {
                        // do not read the state first otherwise the deserialization may throw before we can delete it
                        operation.State.SetState(null);
                        return default;
                    }
                case "DeleteThenThrow":
                    {
                        operation.State.SetState(null);
                        ThrowTestException();
                        return default;
                    }
                case "Throw":
                    {
                        ThrowTestException();
                        return default;
                    }
                case "ThrowNested":
                    {
                        try
                        { 
                            ThrowTestException();
                        }
                        catch (Exception e)
                        {
                            throw new Exception("KABOOOOOM", e);
                        }
                        return default;
                    }
                case "Get":
                    {
                        return GetOrCreate().Value;
                    }
                case "GetNumberIncrementsSent":
                    {
                        return GetOrCreate().NumberIncrementsSent;
                    }
                case "Set":
                    {
                        State state = GetOrCreate();
                        state.Value = (int)operation.GetInput(typeof(int))!;
                        operation.State.SetState(state);
                        return default;
                    }
                case "SetToUnserializable":
                    {
                        State state = GetOrCreate();
                        state.ProblematicObject = CustomSerialization.CreateUnserializableObject();
                        operation.State.SetState(state);
                        return default;
                    }
                case "SetToUndeserializable":
                    {
                        State state = GetOrCreate();
                        state.ProblematicObject = CustomSerialization.CreateUndeserializableObject();
                        operation.State.SetState(state);
                        return default;
                    }
                case "SetThenThrow":
                    {
                        State state = GetOrCreate();
                        state.Value = (int)operation.GetInput(typeof(int))!;
                        operation.State.SetState(state);
                        ThrowTestException();
                        return default;
                    }
                case "Send":
                    {
                        State state = GetOrCreate();
                        EntityInstanceId entityId = (EntityInstanceId)operation.GetInput(typeof(EntityId))!;
                        await state.Send(entityId, operation.Context);
                        operation.State.SetState(state);
                        return default;
                    }
                case "SendThenThrow":
                    {
                        State state = GetOrCreate();
                        EntityInstanceId entityId = (EntityInstanceId)operation.GetInput(typeof(EntityId))!;
                        await state.Send(entityId, operation.Context);
                        operation.State.SetState(state);
                        ThrowTestException();
                        return default;
                    }
                case "SendThenMakeUnserializable":
                    {
                        State state = GetOrCreate();
                        EntityInstanceId entityId = (EntityInstanceId)operation.GetInput(typeof(EntityId))!;
                        await state.Send(entityId, operation.Context);
                        state.ProblematicObject = CustomSerialization.CreateUnserializableObject();
                        operation.State.SetState(state);
                        return default;
                    }
                default:
                    {
                        throw new InvalidOperationException($"undefined entity operation: {operation.Name}");
                    }
            }
        }
    }
}
