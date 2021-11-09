// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestEntities
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();

        //-------------- a very simple entity that stores a string -----------------
        // it offers two operations:
        // "set" (takes a string, assigns it to the current state, does not return anything)
        // "get" (returns a string containing the current state)

        public static void StringStoreEntity([EntityTrigger(EntityName = "StringStore")] IDurableEntityContext context)
        {
            switch (context.OperationName)
            {
                case "set":
                    context.SetState(context.GetInput<string>());
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
                    context.DeleteState();
                    break;

                case "set":
                    context.SetState(context.GetInput<string>());
                    break;

                case "get":
                    if (!context.HasState)
                    {
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
                    context.SetState(context.GetState<int>() + context.GetInput<int>());
                    break;

                case "get":
                    context.Return(context.GetState<int>());
                    break;

                case "set":
                    context.SetState(context.GetInput<int>());
                    break;

                case "delete":
                    context.DeleteState();
                    break;

                default:
                    throw new NotImplementedException("no such entity operation");
            }
        }

        //-------------- An entity that forwards a signal -----------------

        public static void RelayEntity([EntityTrigger(EntityName = "Relay")] IDurableEntityContext context)
        {
            var (destination, operation) = context.GetInput<(EntityId, string)>();

            context.SignalEntity(destination, operation);
        }

        //-------------- An entity representing a phone book, using an untyped json object -----------------

        public static void PhoneBookEntity([EntityTrigger(EntityName = "PhoneBook")] IDurableEntityContext context)
        {
            if (!context.HasState)
            {
                context.SetState(new JObject());
            }

            var state = context.GetState<JObject>();

            switch (context.OperationName)
            {
                case "set":
                    {
                        var (name, number) = context.GetInput<(int, int)>();
                        state[name] = number;
                        break;
                    }

                case "remove":
                    {
                        var name = context.GetInput<string>();
                        state.Remove(name);
                        break;
                    }

                case "lookup":
                    {
                        var name = context.GetInput<string>();
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
                        context.DeleteState();
                        break;
                    }

                default:
                    throw new NotImplementedException("no such entity operation");
            }
        }

        //-------------- An entity that launches an orchestration -----------------

        public static void LauncherEntity([EntityTrigger(EntityName = "Launcher")] IDurableEntityContext context)
        {
            var (id, done) = context.HasState ? context.GetState<(string, bool)>() : (null, false);

            switch (context.OperationName)
            {
                case "launch":
                    {
                        id = context.StartNewOrchestration(nameof(TestOrchestrations.DelayedSignal), context.EntityId);
                        break;
                    }

                case "done":
                    {
                        done = true;
                        break;
                    }

                case "get":
                    {
                        context.Return(done ? id : null);
                        break;
                    }

                default:
                    throw new NotImplementedException("no such entity operation");
            }

            context.SetState((id, done));
        }

        //-------------- An entity representing a phone book, using a typed C# dictionary -----------------

        public static void PhoneBookEntity2([EntityTrigger(EntityName = "PhoneBook2")] IDurableEntityContext context)
        {
            if (!context.HasState)
            {
                context.SetState(new Dictionary<string, decimal>());
            }

            var state = context.GetState<Dictionary<string, decimal>>();

            switch (context.OperationName)
            {
                case "set":
                    {
                        var (name, number) = context.GetInput<(string, decimal)>();
                        state[name] = number;
                        break;
                    }

                case "remove":
                    {
                        var name = context.GetInput<string>();
                        state.Remove(name);
                        break;
                    }

                case "lookup":
                    {
                        var name = context.GetInput<string>();
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
                        context.DeleteState();
                        break;
                    }

                default:
                    throw new NotImplementedException("no such entity operation");
            }
        }

        //-------------- An entity that records all operation names in a list -----------------

        public static void SchedulerEntity(
            [EntityTrigger(EntityName = "SchedulerEntity")] IDurableEntityContext context,
            ILogger logger)
        {
            var state = context.GetState<List<string>>(() => new List<string>());

            if (state.Contains(context.OperationName))
            {
                logger.LogError($"duplicate: {context.OperationName}");
            }

            state.Add(context.OperationName);
        }

        //-------------- An entity that records all batch positions and batch sizes -----------------

        public static void BatchEntity(
            [EntityTrigger(EntityName = "BatchEntity")] IDurableEntityContext context,
            ILogger logger)
        {
            var state = context.GetState(() => new List<(int, int)>());
            state.Add((context.BatchPosition, context.BatchSize));
        }

        //-------------- an entity that stores text, and whose state is
        //                  saved/restored to/from storage when the entity is deactivated/activated -----------------
        //
        // it offers three operations:
        // "clear" sets the current value to empty
        // "append" appends the string provided in the content to the current value
        // "get" returns the current value
        // "deactivate" destructs the entity (after saving its current state in the backing storage)

        public static async Task BlobBackedTextStoreEntity(
            [EntityTrigger(EntityName = "BlobBackedTextStore")] IDurableEntityContext context)
        {
            if (!context.HasState)
            {
                // try to load state from existing blob
                var currentFileContent = await TestHelpers.LoadStringFromTextBlobAsync(
                         context.EntityKey);
                context.SetState(new StringBuilder(currentFileContent ?? ""));
            }

            var state = context.GetState<StringBuilder>();

            switch (context.OperationName)
            {
                case "clear":
                    state.Clear();
                    break;

                case "append":
                    state.Append(context.GetInput<string>());
                    break;

                case "get":
                    context.Return(state.ToString());
                    break;

                case "deactivate":
                    // first, store the current value in a blob
                    await TestHelpers.WriteStringToTextBlob(
                        context.EntityKey, state.ToString());

                    // then, destruct this entity (and all of its state)
                    context.DeleteState();
                    break;

                default:
                    throw new NotImplementedException("no such operation");
            }
        }

        public static async Task HttpEntity(
            [EntityTrigger(EntityName = "HttpEntity")] IDurableEntityContext context,
            ILogger log)
        {
            if (!context.HasState)
            {
                context.SetState(new Dictionary<string, int>());
            }

            Dictionary<string, int> callHistory = context.GetState<Dictionary<string, int>>();

            string requestUri = context.GetInput<string>();

            log.LogInformation($"Calling {requestUri}");

            int statusCode = await CallHttpAsync(requestUri);
            callHistory.Add(requestUri, statusCode);
        }

        private static async Task<int> CallHttpAsync(string requestUri)
        {
            using (HttpResponseMessage response = await SharedHttpClient.GetAsync(requestUri))
            {
                return (int)response.StatusCode;
            }
        }
    }
}
