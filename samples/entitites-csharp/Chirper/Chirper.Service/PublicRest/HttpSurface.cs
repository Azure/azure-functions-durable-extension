// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;

namespace Chirper.Service
{
    public static class HttpSurface
    {
        [Function("UserTimelineGet")]
        public static async Task<HttpResponseData> UserTimelineGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/timeline")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string userId,
            CancellationToken cancellationToken)
        {
            Authenticate(req, userId);
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(GetTimeline), userId, cancellation: cancellationToken);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            OrchestrationMetadata timeline;
            try
            {
                timeline = await client.WaitForInstanceCompletionAsync(
                    instanceId, getInputsAndOutputs: true, cancellation: wait.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return await client.CreateCheckStatusResponseAsync(req, instanceId, cancellationToken);
            }

            if (timeline.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Timeline orchestration '{instanceId}' ended with status '{timeline.RuntimeStatus}'.");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(timeline.ReadOutputAs<Chirp[]>(), cancellationToken);
            return response;
        }

        [Function("UserChirpsGet")]
        public static async Task<HttpResponseData> UserChirpsGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/chirps")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string userId,
            CancellationToken cancellationToken)
        {
            Authenticate(req, userId);
            var target = new EntityInstanceId(nameof(UserChirps), userId);
            var chirps = await client.Entities.GetEntityAsync<UserChirps>(target, cancellation: cancellationToken);
            if (chirps == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(chirps.State.Chirps, cancellationToken);
            return response;
        }

        [Function("UserChirpsPost")]
        public static async Task<HttpResponseData> UserChirpsPost(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{userId}/chirps")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string userId,
            CancellationToken cancellationToken)
        {
            Authenticate(req, userId);
            using var reader = new StreamReader(req.Body);
            var chirp = new Chirp()
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Content = await reader.ReadToEndAsync(cancellationToken),
            };
            var target = new EntityInstanceId(nameof(UserChirps), userId);
            await client.Entities.SignalEntityAsync(target, nameof(UserChirps.Add), chirp, cancellation: cancellationToken);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(chirp, cancellationToken);
            return response;
        }

        [Function("UserChirpsDelete")]
        public static async Task<HttpResponseData> UserChirpsDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "user/{userId}/chirps/{timestamp}")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string userId,
            string timestamp,
            CancellationToken cancellationToken)
        {
            Authenticate(req, userId);
            if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new { error = "The timestamp must be a valid date and time." }, cancellationToken);
                return response;
            }

            var target = new EntityInstanceId(nameof(UserChirps), userId);
            await client.Entities.SignalEntityAsync(target, nameof(UserChirps.Remove), parsedTimestamp.UtcDateTime, cancellation: cancellationToken);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [Function("UserFollowsGet")]
        public static async Task<HttpResponseData> UserFollowsGet(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userId}/follows")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string userId,
            CancellationToken cancellationToken)
        {
            Authenticate(req, userId);
            var target = new EntityInstanceId(nameof(UserFollows), userId);
            var follows = await client.Entities.GetEntityAsync<UserFollows>(target, cancellation: cancellationToken);
            if (follows == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(follows.State.FollowedUsers, cancellationToken);
            return response;
        }

        [Function("UserFollowsPost")]
        public static async Task<HttpResponseData> UserFollowsPost(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "user/{userId}/follows/{userId2}")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string userId,
            string userId2,
            CancellationToken cancellationToken)
        {
            Authenticate(req, userId);
            var target = new EntityInstanceId(nameof(UserFollows), userId);
            await client.Entities.SignalEntityAsync(target, nameof(UserFollows.Add), userId2, cancellation: cancellationToken);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        [Function("UserFollowsDelete")]
        public static async Task<HttpResponseData> UserFollowsDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "user/{userId}/follows/{userId2}")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string userId,
            string userId2,
            CancellationToken cancellationToken)
        {
            Authenticate(req, userId);
            var target = new EntityInstanceId(nameof(UserFollows), userId);
            await client.Entities.SignalEntityAsync(target, nameof(UserFollows.Remove), userId2, cancellation: cancellationToken);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        private static void Authenticate(HttpRequestData request, string userId)
        {
            // Stub: validate that the request is coming from this userId
        }
    }
}
