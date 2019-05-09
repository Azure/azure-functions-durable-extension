// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Chirper.Service
{
    // The UserChirps entity stores all the chirps by ONE user.
    // The entity key is the userId.
    public static class UserChirps
    {
        public enum Ops
        {
            Add,
            Remove,
            Get,
        }

        [FunctionName(nameof(UserChirps))]
        public static Task HandleOperation(
            [EntityTrigger] IDurableEntityContext context)
        {
            var posts = context.GetState(() => new List<Chirp>());

            switch (Enum.Parse<Ops>(context.OperationName))
            {
                case Ops.Add:
                    {
                        var chirp = context.GetInput<Chirp>();
                        posts.Add(chirp);
                    }
                    break;

                case Ops.Remove:
                    {
                        var timestamp = context.GetInput<DateTime>();

                        posts.RemoveAll(chirp => chirp.Timestamp == timestamp);
                    }
                    break;

                case Ops.Get:
                    {
                        context.Return(posts);
                    }
                    break;
            }

            return Task.CompletedTask;
        }

    }
}