// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace Chirper.Service
{
    // The UserChirps entity stores all the chirps by ONE user.
    // The entity key is the userId.

    public class UserChirps
    {
        [JsonPropertyName(nameof(Chirps))]
        public List<Chirp> Chirps { get; set; } = new List<Chirp>();

        public void Add(Chirp chirp)
        {
            Chirps.Add(chirp);
        }

        public void Remove(DateTime timestamp)
        {
            Chirps.RemoveAll(chirp => chirp.Timestamp == timestamp);
        }

        public List<Chirp> Get()
        {
            return Chirps;
        }

        // Boilerplate (entry point for the functions runtime)
        [Function(nameof(UserChirps))]
        public static Task HandleEntityOperation([EntityTrigger] TaskEntityDispatcher dispatcher)
        {
            return dispatcher.DispatchAsync<UserChirps>();
        }
    }
}