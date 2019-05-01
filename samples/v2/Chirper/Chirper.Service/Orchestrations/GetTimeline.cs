using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging; 

namespace Chirper.Service
{
    // The GetTimeline orchestration collects all chirps by followed users,
    // and returns it as a list sorted by timestamp.
    public static class GetTimeline
    {
        [FunctionName(nameof(GetTimeline))]
        public static async Task<Chirp[]> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var userId = context.GetInput<string>();

            // call the UserFollows entity to figure out whose chirps should be included
            var followedUsers = await context.CallEntityAsync<List<string>>(
                new EntityId(nameof(UserFollows), userId), 
                nameof(UserFollows.Ops.Get));

            // in parallel, collect all the chirps
            var tasks = followedUsers
                    .Select(id => context.CallEntityAsync<List<Chirp>>(
                        new EntityId(nameof(UserChirps), id),
                        nameof(UserChirps.Ops.Get)))
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