// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace Chirper.Service
{
    // The GetTimeline orchestration collects all chirps by followed users,
    // and returns it as a list sorted by timestamp.
    public static class GetTimeline
    {
        [Function(nameof(GetTimeline))]
        public static async Task<Chirp[]> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var userId = context.GetInput<string>();

            // call the UserFollows entity to figure out whose chirps should be included
            var followedUsers = await context.Entities.CallEntityAsync<List<string>>(
                new EntityInstanceId(nameof(UserFollows), userId), nameof(UserFollows.Get));

            // in parallel, collect all the chirps
            var tasks = followedUsers
                    .Select(id => context.Entities.CallEntityAsync<List<Chirp>>(
                        new EntityInstanceId(nameof(UserChirps), id), nameof(UserChirps.Get)))
                    .ToList();

            await Task.WhenAll(tasks);

            // combine and sort the returned lists of chirps
            var sortedResults = tasks
                .SelectMany(task => task.Result)
                .OrderBy(chirp => chirp.Timestamp);

            return sortedResults.ToArray();
        }
    }
}