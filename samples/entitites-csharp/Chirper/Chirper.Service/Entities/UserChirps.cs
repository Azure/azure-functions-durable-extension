// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Chirper.Service
{
    // The UserChirps entity stores all the chirps by ONE user.
    // The entity key is the userId.

    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class UserChirps : IUserChirps
    {
        [JsonProperty]
        public List<Chirp> Chirps { get; set; } = new List<Chirp>();

        public void Add(Chirp chirp)
        {
            Chirps.Add(chirp);
        }

        public void Remove(DateTime timestamp)
        {
            Chirps.RemoveAll(chirp => chirp.Timestamp == timestamp);
        }

        public Task<List<Chirp>> Get()
        {
            return Task.FromResult(Chirps);
        }

        // Boilerplate (entry point for the functions runtime)
        [FunctionName(nameof(UserChirps))]
        public static Task HandleEntityOperation([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<UserChirps>();
        }
    }
}