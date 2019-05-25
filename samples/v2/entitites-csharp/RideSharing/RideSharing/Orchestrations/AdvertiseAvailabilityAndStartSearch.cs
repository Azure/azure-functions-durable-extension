// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace RideSharing
{
    public static class AdvertiseAvailabilityAndStartSearch
    {
        [FunctionName(nameof(AdvertiseAvailabilityAndStartSearch))]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var (userId, location) = context.GetInput<(string, int)>();

            var userEntity = new EntityId(nameof(UserEntity), userId);

            // first, update the user information to advertise the location.

            await context.CallEntityAsync(userEntity, nameof(UserEntity.SetLocation), location);
            
            // next, try to match this user with 
            // someone who has already advertised their location in a nearby region

            var nearbyRegions = ZipCodes.GetProximityList(location);

            foreach (var region in nearbyRegions)
            {
                var candidates = await context.CallEntityAsync<string[]>(
                    new EntityId(nameof(RegionEntity), region.ToString()),
                    userId.StartsWith("R") ? nameof(RegionEntity.GetAvailableDrivers) : nameof(RegionEntity.GetAvailableRiders));

                foreach (var candidate in candidates)
                {
                    if (await TryFinalizeMatch(userId, candidate, context))
                    {
                        return;
                    }
                }
            }

            // we could not find a match.
            // we will just wait until someone else finds us.
        }

        public static async Task<bool> TryFinalizeMatch(string initiator, string candidate, IDurableOrchestrationContext context)
        {
            var initiatorEntity = new EntityId(nameof(UserEntity), initiator);
            var candidateEntity = new EntityId(nameof(UserEntity), candidate);

            // Check if both users are still available and close enough.
            // To prevent race conditions, we do this in a critical section
            // that locks both users.

            using (await context.LockAsync(initiatorEntity, candidateEntity))
            {
                var initiatorInfo = await context.CallEntityAsync<UserEntity>(initiatorEntity, nameof(UserEntity.Get));
                var candidateInfo = await context.CallEntityAsync<UserEntity>(candidateEntity, nameof(UserEntity.Get));

                if (initiatorInfo.Location == null)
                {
                    // initiator is no longer trying to find a match! No need to keep trying.
                    return true;
                }
                if (candidateInfo.Location == null
                    || !ZipCodes.GetProximityList(initiatorInfo.Location.Value).Contains(candidateInfo.Location.Value))
                {
                    // candidate is no longer eligible
                    return false;
                }

                // match was successful. Create a new ride.

                var driver = initiator.StartsWith("D") ? initiatorInfo : candidateInfo;
                var rider = initiator.StartsWith("R") ? initiatorInfo : candidateInfo;

                var rideInfo = new RideInfo()
                    {
                        RideId = context.NewGuid(),
                        DriverId = driver.UserId,
                        DriverLocation = driver.Location.Value,
                        RiderId = rider.UserId,
                        RiderLocation = rider.Location.Value,
                    };

                // assign both users to the new ride. 
                // (this is happening within the critical section)
                await Task.WhenAll(
                        context.CallEntityAsync(initiatorEntity, nameof(UserEntity.SetRide), rideInfo),
                        context.CallEntityAsync(candidateEntity, nameof(UserEntity.SetRide), rideInfo)
                );

                return true;
            }
        }
    }
}