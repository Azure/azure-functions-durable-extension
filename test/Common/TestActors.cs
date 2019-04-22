﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestEntities
    {
        //-------------- a very simple entity that stores a string -----------------
        // it offers two operations:
        // "set" (takes a string, assigns it to the current state, does not return anything)
        // "get" (returns a string containing the current state)

        public static void StringStoreEntity([EntityTrigger(EntityName = "StringStore")] IDurableEntityContext context)
        {
            switch (context.OperationName)
            {
                case "set":
                    context.SetState(context.GetOperationContent<string>());
                    break;

                case "get":
                    context.Return(context.GetState<string>());
                    break;

                default:
                    throw new NotImplementedException("no such operation");
            }
        }

        //-------------- a slightly less trivial version of the same -----------------
        // as before with two differences:
        // - "get" throws an exception if the entity does not already exist, i.e. state was not set to anything
        // - a new operation "delete" deletes the entity, i.e. clears all state

        public static void StringStoreEntity2([EntityTrigger(EntityName = "StringStore2")] IDurableEntityContext context)
        {
            switch (context.OperationName)
            {
                case "delete":
                    context.DestructOnExit();
                    break;

                case "set":
                    context.SetState(context.GetOperationContent<string>());
                    break;

                case "get":
                    if (context.IsNewlyConstructed)
                    {
                        context.DestructOnExit();
                        throw new InvalidOperationException("must not call get on a non-existing entity");
                    }

                    context.Return(context.GetState<string>());
                    break;

                default:
                    throw new NotImplementedException("no such operation");
            }
        }

        //-------------- An entity representing a counter object -----------------

        public static void CounterEntity([EntityTrigger(EntityName = "Counter")] IDurableEntityContext context)
        {
            switch (context.OperationName)
            {
                case "increment":
                    context.SetState(context.GetState<int>() + 1);
                    break;

                case "add":
                    context.SetState(context.GetState<int>() + context.GetOperationContent<int>());
                    break;

                case "get":
                    context.Return(context.GetState<int>());
                    break;

                case "set":
                    context.SetState(context.GetOperationContent<int>());
                    break;

                case "delete":
                    context.DestructOnExit();
                    break;

                default:
                    throw new NotImplementedException("no such entity operation");
            }
        }

        //-------------- An entity representing a phone book, using an untyped json object -----------------

        public static void PhoneBookEntity([EntityTrigger(EntityName = "PhoneBook")] IDurableEntityContext context)
        {
            if (context.IsNewlyConstructed)
            {
                context.SetState(new JObject());
            }

            var state = context.GetState<JObject>();

            switch (context.OperationName)
            {
                case "set":
                    {
                        var (name, number) = context.GetOperationContent<(int, int)>();
                        state[name] = number;
                        break;
                    }

                case "remove":
                    {
                        var name = context.GetOperationContent<string>();
                        state.Remove(name);
                        break;
                    }

                case "lookup":
                    {
                        var name = context.GetOperationContent<string>();
                        context.Return(state[name]);
                        break;
                    }

                case "dump":
                    {
                        context.Return(state);
                        break;
                    }

                case "clear":
                    {
                        context.DestructOnExit();
                        break;
                    }

                default:
                    throw new NotImplementedException("no such entity operation");
            }
        }

        //-------------- An entity representing a phone book, using a typed C# dictionary -----------------

        public static void PhoneBookEntity2([EntityTrigger(EntityName = "PhoneBook2")] IDurableEntityContext context)
        {
            if (context.IsNewlyConstructed)
            {
                context.SetState(new Dictionary<string, decimal>());
            }

            var state = context.GetState<Dictionary<string, decimal>>();

            switch (context.OperationName)
            {
                case "set":
                    {
                        var (name, number) = context.GetOperationContent<(string, decimal)>();
                        state[name] = number;
                        break;
                    }

                case "remove":
                    {
                        var name = context.GetOperationContent<string>();
                        state.Remove(name);
                        break;
                    }

                case "lookup":
                    {
                        var name = context.GetOperationContent<string>();
                        context.Return(state[name]);
                        break;
                    }

                case "dump":
                    {
                        context.Return(state);
                        break;
                    }

                case "clear":
                    {
                        context.DestructOnExit();
                        break;
                    }

                default:
                    throw new NotImplementedException("no such entity operation");
            }
        }

        //-------------- an entity that stores text, and whose state is
        //                  saved/restored to/from storage when the entity is deactivated/activated -----------------
        //
        // it offers three operations:
        // "clear" sets the current value to empty
        // "append" appends the string provided in the content to the current value
        // "get" returns the current value
        // "deactivate" destructs the entity (after saving its current state in the backing storage)

        public static async Task BlobBackedTextStoreEntity([EntityTrigger(EntityName = "BlobBackedTextStore")] IDurableEntityContext context)
        {
            if (context.IsNewlyConstructed)
            {
                // try to load state from existing blob
                var currentFileContent = await context.CallActivityAsync<string>(
                         nameof(TestActivities.LoadStringFromTextBlob),
                         context.Key);
                context.SetState(new StringBuilder(currentFileContent ?? ""));
            }

            switch (context.OperationName)
            {
                case "clear":
                    context.GetState<StringBuilder>().Clear();
                    break;

                case "append":
                    context.GetState<StringBuilder>().Append(context.GetOperationContent<string>());
                    break;

                case "get":
                    context.Return(context.GetState<StringBuilder>().ToString());
                    break;

                case "deactivate":
                    // first, store the current value in a blob
                    await context.CallActivityAsync(
                        nameof(TestActivities.WriteStringToTextBlob),
                        (context.Key, context.GetState<StringBuilder>().ToString()));

                    // then, destruct this entity (and all of its state)
                    context.DestructOnExit();
                    break;

                default:
                    throw new NotImplementedException("no such operation");
            }
        }

        //-------------- an entity representing a chat room -----------------
        // this example shows how to use reflection to define entities using a C# class.

        public static void ChatRoomEntity([EntityTrigger(EntityName = "ChatRoom")] IDurableEntityContext context)
        {
            // if the entity is fresh call the constructor for the state
            if (context.IsNewlyConstructed)
            {
                context.SetState(new ChatRoom(context));
            }

            // find the method corresponding to the operation
            var method = typeof(ChatRoom).GetMethod(context.OperationName);

            // determine the type of the operation content (= second method argument) and deserialize
            var contentType = method.GetParameters()[1].ParameterType;
            var content = context.GetOperationContent(contentType);

            // invoke the method and return the result;
            var result = method.Invoke(context.GetState<ChatRoom>(), new object[2] { context, content });
            context.Return(result);
        }

        public class ChatRoom
        {
            public ChatRoom(IDurableEntityContext ctx)
            {
                this.ChatEntries = new SortedDictionary<DateTime, string>();
            }

            public SortedDictionary<DateTime, string> ChatEntries { get; set; }

            // an operation that adds a message to the chat
            public DateTime Post(IDurableEntityContext ctx, string content)
            {
                var timestamp = ctx.CurrentUtcDateTime;
                this.ChatEntries.Add(timestamp, content);
                return timestamp;
            }

            // an operation that reads all messages in the chat, within range
            public List<KeyValuePair<DateTime, string>> Read(IDurableEntityContext ctx, DateTime? fromRange)
            {
                if (fromRange.HasValue)
                {
                    return this.ChatEntries.Where(kvp => kvp.Key >= fromRange.Value).ToList();
                }
                else
                {
                    return this.ChatEntries.ToList();
                }
            }
        }
    }
}
