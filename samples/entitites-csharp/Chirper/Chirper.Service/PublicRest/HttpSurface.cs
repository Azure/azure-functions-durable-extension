// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Chirper.Service
{
    public static class HttpSurface
    {
        [FunctionName("UserTimelineGet")]
        public static async Task<HttpResponseMessage> UserTimelineGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/timeline")] HttpRequestMessage req,
            [DurableClient] IDurableClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var instanceId = await client.StartNewAsync<string>(nameof(GetTimeline), userId);
            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId);
        }

        [FunctionName("UserChirpsGet")]
        public static async Task<HttpResponseMessage> UserChirpsGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/chirps")] HttpRequestMessage req,
            [DurableClient] IDurableClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserChirps), userId);
            var chirps = await client.ReadEntityStateAsync<UserChirps>(target);
            return chirps.EntityExists
                    ? req.CreateResponse(HttpStatusCode.OK, chirps.EntityState.Chirps)
                    : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [FunctionName("UserChirpsPost")]
        public static async Task<HttpResponseMessage> UserChirpsPost(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{userId}/chirps")] HttpRequestMessage req,
            [DurableClient] IDurableClient client,
            ILogger log, 
            string userId)
        {
            Authenticate(req, userId);
            var chirp = new Chirp()
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Content = await req.Content.ReadAsStringAsync(),
            };
            await client.SignalEntityAsync<IUserChirps>(userId, x => x.Add(chirp));
            return req.CreateResponse(HttpStatusCode.Accepted, chirp);
        }

        [FunctionName("UserChirpsDelete")]
        public static async Task<HttpResponseMessage> UserChirpsDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "user/{userId}/chirps/{timestamp}")] HttpRequestMessage req,
            [DurableClient] IDurableClient client,
            ILogger log,
            string userId,
            DateTime timestamp)
        {
            Authenticate(req, userId);
            await client.SignalEntityAsync<IUserChirps>(userId, x => x.Remove(timestamp));
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [FunctionName("UserFollowsGet")]
        public static async Task<HttpResponseMessage> UserFollowsGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/follows")] HttpRequestMessage req,
            [DurableClient] IDurableClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserFollows), userId);
            var follows = await client.ReadEntityStateAsync<UserFollows>(target);
            return follows.EntityExists
                    ? req.CreateResponse(HttpStatusCode.OK, follows.EntityState.FollowedUsers)
                    : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [FunctionName("UserFollowsPost")]
        public static async Task<HttpResponseMessage> UserFollowsPost(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{userId}/follows/{userId2}")] HttpRequestMessage req,
            [DurableClient] IDurableClient client,
            ILogger log,
            string userId,
            string userId2)
        {
            Authenticate(req, userId);
            await client.SignalEntityAsync<IUserFollows>(userId, x => x.Add(userId2));
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [FunctionName("UserFollowsDelete")]
        public static async Task<HttpResponseMessage> UserFollowsDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "user/{userId}/follows/{userId2}")] HttpRequestMessage req,
            [DurableClient] IDurableClient client,
            ILogger log,
            string userId,
            string userId2)
        {
            Authenticate(req, userId);
            var content = await req.Content.ReadAsAsync<string>();
            await client.SignalEntityAsync<IUserFollows>(userId, x => x.Remove(userId2));
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        private static void Authenticate(HttpRequestMessage request, string userId)
        {
            // Stub: validate that the request is coming from this userId
        }

    }
}
