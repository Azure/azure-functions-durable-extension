// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace Chirper.Service
{
    // The UserFollows entity stores all the follows of ONE user.
    // The entity key is the userId.

    public class UserFollows
    {
        [JsonPropertyName(nameof(FollowedUsers))]
        public List<string> FollowedUsers { get; set; } = new List<string>();

        public void Add(string user)
        {
            FollowedUsers.Add(user);
        }

        public void Remove(string user)
        {
            FollowedUsers.Remove(user);
        }

        public List<string> Get()
        {
            return FollowedUsers;
        }

        // Boilerplate (entry point for the functions runtime)
        [Function(nameof(UserFollows))]
        public static Task HandleEntityOperation([EntityTrigger] TaskEntityDispatcher dispatcher)
        {
            return dispatcher.DispatchAsync<UserFollows>();
        }
    }
}