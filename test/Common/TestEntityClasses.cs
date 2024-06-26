// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

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

        public interface IJob
        {
            void SetStartDate(DateTime date);

            void SetEndDate(DateTime date);

            Task<TimeSpan> GetDuration();

            void Delete();
        }

        public interface IPrimaryJob : IJob
        {
            void SetId(string id);

            Task<string> GetId();
        }

        public interface ISelfSchedulingEntity
        {
            void Start();

            void A();

            Task B();

            void C();

            Task<int> D();
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

        [FunctionName(nameof(JobWithProxyMultiInterface))]
        public static Task JobFunction([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<JobWithProxyMultiInterface>();
        }

        [FunctionName(nameof(StorageBackedCounter))]
        public static Task StorageBackedCounterFunction(
            [EntityTrigger] IDurableEntityContext context,
            [Blob(BlobContainerPath)] BlobContainerClient blobContainer)
        {
            return context.DispatchAsync<StorageBackedCounter>(blobContainer);
        }

        [FunctionName(nameof(SelfSchedulingEntity))]
        public static Task SelfSchedulingEntityFunction([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<SelfSchedulingEntity>();
        }

#pragma warning disable DF0305 // Function named 'ClassBasedFaultyEntity' doesn't have an entity class with the same name defined. Did you mean 'FaultyEntity'?
#pragma warning disable DF0307 // DispatchAsync must be used with the entity name, equal to the name of the function it's used in.
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
            else if (context.OperationName == "delay")
            {
                return Task.Delay(TimeSpan.FromSeconds(context.GetInput<int>()));
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

                case "delay":
                    return Task.Delay(TimeSpan.FromSeconds(context.GetInput<int>()));

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
#pragma warning restore DF0307 // DispatchAsync must be used with the entity name, equal to the name of the function it's used in.
#pragma warning restore DF0305

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

        //-------------- An entity that schedules itself ------------------
        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class SelfSchedulingEntity : ISelfSchedulingEntity
        {
            [JsonProperty]
            public string Value { get; set; } = "";

            public void Start()
            {
                var now = DateTime.UtcNow;

                var timeA = now + TimeSpan.FromSeconds(1);
                var timeB = now + TimeSpan.FromSeconds(2);
                var timeC = now + TimeSpan.FromSeconds(3);
                var timeD = now + TimeSpan.FromSeconds(4);

                Entity.Current.SignalEntity<ISelfSchedulingEntity>(Entity.Current.EntityId.EntityKey, timeD, p => p.D());
                Entity.Current.SignalEntity<ISelfSchedulingEntity>(Entity.Current.EntityId.EntityKey, timeC, p => p.C());
                Entity.Current.SignalEntity<ISelfSchedulingEntity>(Entity.Current.EntityId.EntityKey, timeB, p => p.B());
                Entity.Current.SignalEntity<ISelfSchedulingEntity>(Entity.Current.EntityId.EntityKey, timeA, p => p.A());
            }

            public void A()
            {
                this.Value += "A";
            }

            public Task B()
            {
                this.Value += "B";
                return Task.Delay(100);
            }

            public void C()
            {
                this.Value += "C";
            }

            public Task<int> D()
            {
                this.Value += "D";
                return Task.FromResult(111);
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
            private const string BlobName = "counter";

            private readonly BlobContainerClient blobContainer;

            public StorageBackedCounter(BlobContainerClient blobContainer)
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
                BlockBlobClient environmentVariableBlob = this.blobContainer.GetBlockBlobClient(BlobName);
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
                BlockBlobClient environmentVariableBlob = this.blobContainer.GetBlockBlobClient(BlobName);
                using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes(newValue.ToString())))
                {
                    await environmentVariableBlob.UploadAsync(buffer);
                }
            }

            public void Delete()
            {
                Entity.Current.DeleteState();
            }
        }

        //-------------- An entity representing a job object with multiple interfaces -----------------
        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class JobWithProxyMultiInterface : IPrimaryJob
        {
            [JsonProperty("id")]
            public string Id { get; private set; }

            [JsonProperty("startDate")]
            public DateTime StartDate { get; private set; }

            [JsonProperty("endDate")]
            public DateTime EndDate { get; private set; }

            public void SetId(string id) => this.Id = id;

            public void SetEndDate(DateTime date) => this.EndDate = date;

            public void SetStartDate(DateTime date) => this.StartDate = date;

            public Task<string> GetId() => Task.FromResult(this.Id);

            public Task<TimeSpan> GetDuration() => Task.FromResult(this.EndDate - this.StartDate);

            public void Delete()
            {
                Entity.Current.DeleteState();
            }

            [FunctionName(nameof(JobWithProxyMultiInterface))]
            public static Task Run(
            [EntityTrigger] IDurableEntityContext context)
            {
                return context.DispatchAsync<JobWithProxyMultiInterface>();
            }
        }

        //-------------- an entity that requires custom deserialization settings to work

        public class EntityWithPrivateSetter
        {
            public int Value { get; private set; }

            public int Get() => this.Value;

            public void Inc() => this.Value++;
        }

        public class PrivateResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty prop = base.CreateProperty(member, memberSerialization);

                if (!prop.Writable)
                {
                    var property = member as PropertyInfo;
                    var hasPrivateSetter = property?.GetSetMethod(true) != null;
                    prop.Writable = hasPrivateSetter;
                }

                return prop;
            }
        }

        internal class CustomMessageSerializerSettingsFactory : IMessageSerializerSettingsFactory
        {
            public JsonSerializerSettings CreateJsonSerializerSettings()
            {
                return new JsonSerializerSettings()
                {
                    ContractResolver = new PrivateResolver(),
                    TypeNameHandling = TypeNameHandling.None,
                    DateParseHandling = DateParseHandling.None,
                };
            }
        }
    }
}
