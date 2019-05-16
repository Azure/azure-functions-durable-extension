// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using System.Linq;

namespace RideSharing
{
    public static class HttpSurface
    {
        [FunctionName("UserStatusGet")]
        public static async Task<HttpResponseMessage> UserInfoGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/status")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserEntity), userId);
            var response = await client.ReadEntityStateAsync<UserStatus>(target);
            return response.EntityExists
                    ? req.CreateResponse(HttpStatusCode.OK, response.EntityState)
                    : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [FunctionName("UserPostAvailable")]
        public static async Task<HttpResponseMessage> UserPostAvailable(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{userId}/available")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            string locationString = req.RequestUri.ParseQueryString().Get("location");
            if (string.IsNullOrEmpty(locationString) || !int.TryParse(locationString, out var location))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "query must include integer location");
            }
            var instanceId = await client.StartNewAsync(nameof(AdvertiseAvailabilityAndStartSearch), (userId, location));
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("UserDeleteAvailable")]
        public static async Task<HttpResponseMessage> UserDeleteAvailable(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "user/{userId}/available")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string userId)
        {
            Authenticate(req, userId);
            var target = new EntityId(nameof(UserEntity), userId);
            await client.SignalEntityAsync(target, "set-location", (int?) null);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [FunctionName("DriverPostComplete")]
        public static async Task<HttpResponseMessage> DriverPostComplete(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{driverId}/completed")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            string driverId)
        {
            Authenticate(req, driverId);
            if (! driverId.StartsWith("D"))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "only drivers can call this");
            }
            var rideIdString = req.RequestUri.ParseQueryString().Get("rideId");
            if (string.IsNullOrEmpty(rideIdString) || !Guid.TryParse(rideIdString, out var rideId))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "query must include a rideId Guid");
            }
            var driverEntity = new EntityId(nameof(UserEntity), driverId);
            await client.SignalEntityAsync(driverEntity, "clear-ride", rideId);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [FunctionName("RegionGet")]
        public static async Task<HttpResponseMessage> RegionGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "region/{location}")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log,
            int location)
        {
            var regionEntity = new EntityId(nameof(RegionEntity), location.ToString());
            var response = await client.ReadEntityStateAsync<HashSet<string>>(regionEntity);
            return response.EntityExists
                    ? req.CreateResponse(HttpStatusCode.OK, response.EntityState.ToArray())
                    : req.CreateResponse(HttpStatusCode.NotFound);
        }

        private static void Authenticate(HttpRequestMessage request, string userId)
        {
            // Stub: validate that the request is coming from this userId
        }

         
    }
}
