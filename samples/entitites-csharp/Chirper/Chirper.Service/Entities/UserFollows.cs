// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Chirper.Service
{
    // The UserFollows entity stores all the follows of ONE user.
    // The entity key is the userId.

    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class UserFollows : IUserFollows
    {
        [JsonProperty]
        public List<string> FollowedUsers { get; set; }  = new List<string>();

        public void Add(string user)
        {
            FollowedUsers.Add(user);
        }

        public void Remove(string user)
        {
            FollowedUsers.Remove(user);
        }

        public Task<List<string>> Get()
        {
            return Task.FromResult(FollowedUsers);
        }

        // Boilerplate (entry point for the functions runtime)
        [FunctionName(nameof(UserFollows))]
        public static Task HandleEntityOperation([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<UserFollows>();    
        }
    }
}