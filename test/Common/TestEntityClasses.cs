// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TestEntityClasses
    {
        // this example shows how to use the C# class API for entities.
        public const string BlobContainerPath = "durable-entities-binding-test";

        public interface IChatRoom
        {
            DateTime Post(string content);

            List<KeyValuePair<DateTime, string>> Get();
        }

        public interface ICounter
        {
            void Increment();

            void Add(int value);

            Task<int> Get();

            void Set(int newValue);

            void Delete();
        }

        public interface IAsyncCounter
        {
            Task Increment();

            Task Add(int value);

            Task<int> Get();

            Task Set(int newValue);

            void Delete();
        }

        public interface IFaultyEntity
        {
            Task<int> Get();

            Task<int> GetNumberIncrementsSent();

            Task Set(int value);

            Task SetToUnserializable();

            Task SetToUnDeserializable();

            Task SetThenThrow(int value);

            Task Send(EntityId target);

            Task SendThenThrow(EntityId target);

            Task SendThenMakeUnserializable(EntityId target);

            Task Delete();

            Task DeleteThenThrow();

            Task Throw();

            Task ThrowUnserializable();

            Task ThrowUnDeserializable();
        }

        [FunctionName(nameof(ChatRoom))]
        public static Task ChatRoomFunction([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<ChatRoom>();
        }

        [FunctionName(nameof(CounterWithProxy))]
        public static Task CounterFunction([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<CounterWithProxy>();
        }

        [FunctionName(nameof(StorageBackedCounter))]
        public static Task StorageBackedCounterFunction([EntityTrigger] IDurableEntityContext context, [Blob(BlobContainerPath)] CloudBlobContainer blobContainer)
        {
            return context.DispatchAsync<StorageBackedCounter>(blobContainer);
        }

        [FunctionName("ClassBasedFaultyEntity")]
        public static Task FaultyEntityFunction([EntityTrigger] IDurableEntityContext context)
        {
            // we use an untyped call to test existence without creating the entity
            if (context.OperationName == "exists")
            {
                context.Return(context.HasState);
                return Task.CompletedTask;
            }
            else if (context.OperationName == "deletewithoutreading")
            {
                context.DeleteState();
                return Task.CompletedTask;
            }

            return context.DispatchAsync<FaultyEntity>();
        }

        [FunctionName("FunctionBasedFaultyEntity")]
        public static Task FaultyEntityFunctionWithoutDispatch([EntityTrigger] IDurableEntityContext context)
        {
            switch (context.OperationName)
            {
                case "exists":
                    context.Return(context.HasState);
                    break;

                case "deletewithoutreading":
                    context.DeleteState();
                    break;

                case "Get":
                    if (!context.HasState)
                    {
                        context.Return(0);
                    }
                    else
                    {
                        context.Return(context.GetState<FaultyEntity>().Value);
                    }

                    break;

                case "GetNumberIncrementsSent":
                    context.Return(context.GetState<FaultyEntity>().NumberIncrementsSent);
                    break;

                case "Set":
                    var state = context.GetState<FaultyEntity>() ?? new FaultyEntity();
                    state.Value = context.GetInput<int>();
                    context.SetState(state);
                    break;

                case "SetToUnserializable":
                    var state1 = context.GetState<FaultyEntity>() ?? new FaultyEntity();
                    state1.SetToUnserializable();
                    context.SetState(state1);
                    break;

                case "SetToUnDeserializable":
                    var state2 = context.GetState<FaultyEntity>() ?? new FaultyEntity();
                    state2.SetToUnDeserializable();
                    context.SetState(state2);
                    break;

                case "SetThenThrow":
                    var state3 = context.GetState<FaultyEntity>() ?? new FaultyEntity();
                    state3.Value = context.GetInput<int>();
                    context.SetState(state3);
                    throw new FaultyEntity.SerializableKaboom();

                case "Send":
                    var state4 = context.GetState<FaultyEntity>() ?? new FaultyEntity();
                    state4.Send(context.GetInput<EntityId>());
                    context.SetState(state4);
                    return Task.CompletedTask;

                case "SendThenThrow":
                    var state5 = context.GetState<FaultyEntity>() ?? new FaultyEntity();
                    state5.Send(context.GetInput<EntityId>());
                    context.SetState(state5);
                    throw new FaultyEntity.SerializableKaboom();

                case "SendThenMakeUnserializable":
                    var state6 = context.GetState<FaultyEntity>() ?? new FaultyEntity();
                    state6.Send(context.GetInput<EntityId>());
                    context.SetState(state6);
                    state6.SetToUnserializable();
                    return Task.CompletedTask;

                case "Delete":
                    context.DeleteState();
                    break;

                case "DeleteThenThrow":
                    context.DeleteState();
                    throw new FaultyEntity.SerializableKaboom();

                case "Throw":
                    throw new FaultyEntity.SerializableKaboom();

                case "ThrowUnserializable":
                    throw new FaultyEntity.UnserializableKaboom();

                case "ThrowUnDeserializable":
                    throw new FaultyEntity.UnDeserializableKaboom();
            }

            return Task.CompletedTask;
        }

        //-------------- an entity representing a chat room -----------------

        [JsonObject(MemberSerialization.OptIn)]
        public class ChatRoom : IChatRoom
        {
            public ChatRoom()
            {
                this.ChatEntries = new SortedDictionary<DateTime, string>();
            }

            [JsonProperty("chatEntries")]
            public SortedDictionary<DateTime, string> ChatEntries { get; set; }

            // an operation that adds a message to the chat
            public DateTime Post(string content)
            {
                // create a monotonically increasing timestamp
                var lastPost = this.ChatEntries.LastOrDefault().Key;
                var timestamp = new DateTime(Math.Max(DateTime.UtcNow.Ticks, lastPost.Ticks + 1));

                this.ChatEntries.Add(timestamp, content);

                return timestamp;
            }

            // an operation that reads all messages in the chat
            public List<KeyValuePair<DateTime, string>> Get()
            {
                return this.ChatEntries.ToList();
            }
        }

        //-------------- An entity representing a counter object -----------------

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class CounterWithProxy : ICounter
        {
            [JsonProperty("value")]
            public int Value { get; set; }

            public void Increment()
            {
                this.Value += 1;
            }

            public void Add(int value)
            {
                this.Value += value;
            }

            public Task<int> Get()
            {
                return Task.FromResult(this.Value);
            }

            public void Set(int newValue)
            {
                this.Value = newValue;
            }

            public void Delete()
            {
                Entity.Current.DeleteState();
            }
        }

        //-------------- An entity that throws exceptions -----------------

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class FaultyEntity : IFaultyEntity
        {
            [JsonProperty]
            public int Value { get; set; }

            [JsonProperty]
            [JsonConverter(typeof(CustomJsonConverter))]
            public object ObjectWithFaultySerialization { get; set; }

            [JsonProperty]
            public int NumberIncrementsSent { get; set; }

            public Task<int> Get()
            {
                return Task.FromResult(this.Value);
            }

            public Task<int> GetNumberIncrementsSent()
            {
                return Task.FromResult(this.NumberIncrementsSent);
            }

            public Task Set(int value)
            {
                this.Value = value;
                return Task.CompletedTask;
            }

            public Task Delete()
            {
                Entity.Current.DeleteState();
                return Task.CompletedTask;
            }

            public Task SetToUnserializable()
            {
                this.ObjectWithFaultySerialization = new UnserializableKaboom();
                return Task.CompletedTask;
            }

            public Task SetToUnDeserializable()
            {
                this.ObjectWithFaultySerialization = new UnDeserializableKaboom();
                return Task.CompletedTask;
            }

            public Task Send(EntityId target)
            {
                var desc = $"{++this.NumberIncrementsSent}:{this.Value}";
                Entity.Current.SignalEntity(target, desc);
                return Task.CompletedTask;
            }

            public Task Throw()
            {
                this.Throw(true, true);
                return Task.CompletedTask;
            }

            public Task ThrowUnserializable()
            {
                this.Throw(false, false);
                return Task.CompletedTask;
            }

            public Task ThrowUnDeserializable()
            {
                this.Throw(true, false);
                return Task.CompletedTask;
            }

            public Task SetThenThrow(int value)
            {
                this.Value = value;
                this.Throw(false, false);
                return Task.CompletedTask;
            }

            public Task SendThenThrow(EntityId target)
            {
                this.Send(target);
                this.Throw(false, false);
                return Task.CompletedTask;
            }

            public Task SendThenMakeUnserializable(EntityId target)
            {
                this.Send(target);
                this.ObjectWithFaultySerialization = new UnserializableKaboom();
                return Task.CompletedTask;
            }

            public Task DeleteThenThrow()
            {
                Entity.Current.DeleteState();
                this.Throw(false, false);
                return Task.CompletedTask;
            }

            private Task Throw(bool serializable, bool deserializable)
            {
                if (serializable)
                {
                    if (deserializable)
                    {
                        throw new SerializableKaboom();
                    }
                    else
                    {
                        throw new UnDeserializableKaboom();
                    }
                }
                else
                {
                    throw new UnserializableKaboom();
                }
            }

            public class CustomJsonConverter : JsonConverter
            {
                public override bool CanConvert(Type objectType)
                {
                    return objectType == typeof(SerializableKaboom)
                        || objectType == typeof(UnserializableKaboom)
                        || objectType == typeof(UnDeserializableKaboom);
                }

                public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                {
                    if (reader.TokenType == JsonToken.Null)
                    {
                        return null;
                    }

                    var typename = serializer.Deserialize<string>(reader);

                    if (typename != nameof(SerializableKaboom))
                    {
                        throw new JsonSerializationException("not deserializable");
                    }

                    return new SerializableKaboom();
                }

                public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                {
                    if (value is UnserializableKaboom)
                    {
                        throw new JsonSerializationException("not serializable");
                    }

                    serializer.Serialize(writer, value.GetType().Name);
                }
            }

            public class UnserializableKaboom : Exception
            {
            }

            public class SerializableKaboom : Exception
            {
            }

            public class UnDeserializableKaboom : Exception
            {
            }
        }

        //-------------- An entity representing a counter object -----------------

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class StorageBackedCounter : IAsyncCounter
        {
            private readonly CloudBlobContainer blobContainer;
            private readonly string blobName = "counter";

            public StorageBackedCounter(CloudBlobContainer blobContainer)
            {
                this.blobContainer = blobContainer;
            }

            [JsonProperty("value")]
            public int Value { get; set; }

            public async Task Increment()
            {
                await this.Add(1);
            }

            public async Task Add(int value)
            {
                int currValue = await this.Get();
                await this.Set(currValue + value);
            }

            public async Task<int> Get()
            {
                CloudBlockBlob environmentVariableBlob = this.blobContainer.GetBlockBlobReference(this.blobName);
                if (await environmentVariableBlob.ExistsAsync())
                {
                    var readStream = await environmentVariableBlob.OpenReadAsync();
                    using (var reader = new StreamReader(readStream))
                    {
                        string storedValueString = await reader.ReadToEndAsync();
                        int storedValue = JToken.Parse(storedValueString).ToObject<int>();
                        if (this.Value != storedValue)
                        {
                            throw new InvalidOperationException("Local state and blob state do not match.");
                        }

                        return this.Value;
                    }
                }
                else
                {
                    return this.Value;
                }
            }

            public async Task Set(int newValue)
            {
                this.Value = newValue;
                CloudBlockBlob environmentVariableBlob = this.blobContainer.GetBlockBlobReference(this.blobName);
                await environmentVariableBlob.UploadTextAsync(newValue.ToString());
            }

            public void Delete()
            {
                Entity.Current.DeleteState();
            }
        }
    }
}
