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

            await context.CallEntityAsync(userEntity, "set-location", location);
            
            // next, try to match this user with 
            // someone who has already advertised their location in a nearby region

            var nearbyRegions = ZipCodes.GetProximityList(location);

            foreach (var region in nearbyRegions)
            {
                var candidates = await context.CallEntityAsync<string[]>(
                    new EntityId(nameof(RegionEntity), region.ToString()),
                    userId.StartsWith("R") ? "get-available-drivers" : "get-available-riders");

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

        public static async Task<bool> TryFinalizeMatch(string user1, string user2, IDurableOrchestrationContext context)
        {
            var user1Entity = new EntityId(nameof(UserEntity), user1);
            var user2Entity = new EntityId(nameof(UserEntity), user2);

            // Check if both users are still available and close enough.
            // To prevent race conditions, we do this in a critical section
            // that locks both users.

            using (await context.LockAsync(user1Entity, user2Entity))
            {
                var info1 = await context.CallEntityAsync<UserStatus>(user1Entity, "get");
                var info2 = await context.CallEntityAsync<UserStatus>(user2Entity, "get");

                if (info1.Location == null)
                {
                    // the user1 is no longer trying to find a match! No need to keep trying.
                    return true;
                }
                if (info2.Location == null
                    || !ZipCodes.GetProximityList(info1.Location.Value).Contains(info2.Location.Value))
                {
                    // user2 is no longer eligible
                    return false;
                }

                // match was successful. Create a new ride.

                var driver = user1.StartsWith("D") ? info1 : info2;
                var rider = user1.StartsWith("R") ? info1 : info2;

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
                        context.CallEntityAsync(user1Entity, "set-ride", rideInfo),
                        context.CallEntityAsync(user2Entity, "set-ride", rideInfo)
                );

                return true;
            }
        }
    }
}