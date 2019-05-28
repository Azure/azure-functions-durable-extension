// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TestEntityClasses
    {
        //-------------- an entity representing a chat room -----------------
        // this example shows how to use the C# class API for entities.

        public interface IChatRoom
        {
            DateTime Post(string content);

            List<KeyValuePair<DateTime, string>> Get();
        }

        [FunctionName(nameof(ChatRoom))]
        public static Task ChatRoomFunction([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<ChatRoom>();
        }

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
                var timestamp = DateTime.UtcNow;
                this.ChatEntries.Add(timestamp, content);
                return timestamp;
            }

            // an operation that reads all messages in the chat
            public List<KeyValuePair<DateTime, string>> Get()
            {
                return this.ChatEntries.ToList();
            }
        }
    }
}
