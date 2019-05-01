using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using System.Linq;

namespace Chirper.Service
{
    public static class HttpSurface
    {
        [FunctionName("UserTimelineGet")]
        public static async Task<HttpResponseMessage> Run1(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/timeline")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var instanceId = await client.StartNewAsync(nameof(GetTimeline), userId);
            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId);
        }

        [FunctionName("UserChirpsGet")]
        public static async Task<HttpResponseMessage> Run2(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/chirps")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserChirps), userId);
            var chirps = await client.ReadEntityStateAsync<List<Chirp>>(target);
            return chirps.EntityExists
                    ? req.CreateResponse(HttpStatusCode.OK, (Chirp[]) chirps.EntityState.ToArray())
                    : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [FunctionName("UserChirpsPost")]
        public static async Task<HttpResponseMessage> Run3(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{userId}/chirps")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log, 
            string userId)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserChirps), userId);
            var chirp = new Chirp()
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Content = await req.Content.ReadAsStringAsync(),
            };
            await client.SignalEntityAsync(target, nameof(UserChirps.Ops.Add), chirp);
            return req.CreateResponse(HttpStatusCode.Accepted, chirp);
        }

        [FunctionName("UserChirpsDelete")]
        public static async Task<HttpResponseMessage> Run4(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "user/{userId}/chirps/{timestamp}")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId,
            DateTime timestamp)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserChirps), userId);
            await client.SignalEntityAsync(target, nameof(UserChirps.Ops.Remove), timestamp);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [FunctionName("UserFollowsGet")]
        public static async Task<HttpResponseMessage> Run5(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/follows")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserFollows), userId);
            await client.SignalEntityAsync(target, nameof(UserFollows.Ops.Get));
            var follows = await client.ReadEntityStateAsync<HashSet<string>>(target);
            return follows.EntityExists
                    ? req.CreateResponse(HttpStatusCode.OK, (string[]) follows.EntityState.ToArray())
                    : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [FunctionName("UserFollowsPost")]
        public static async Task<HttpResponseMessage> Run6(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{userId}/follows/{userId2}")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId,
            string userId2)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserFollows), userId);
            await client.SignalEntityAsync(target, nameof(UserFollows.Ops.Add), userId2);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [FunctionName("UserFollowsDelete")]
        public static async Task<HttpResponseMessage> Run7(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "user/{userId}/follows/{userId2}")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId,
            string userId2)
        {
            Authenticate(req, userId);
            var content = await req.Content.ReadAsAsync<string>();
            var target = new EntityId(nameof(UserFollows), userId);
            await client.SignalEntityAsync(target, nameof(UserFollows.Ops.Remove), userId2);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        private static void Authenticate(HttpRequestMessage request, string userId)
        {
            // Stub: validate that the request is coming from this userId
        }

    }
}
