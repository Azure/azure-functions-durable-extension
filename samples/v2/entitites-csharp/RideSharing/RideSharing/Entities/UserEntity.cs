// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace RideSharing
{
    // The UserEntity tracks the current state of a particular user (driver or rider). 
    // There is one UserEntity entity per user. 
    // The entity key is the UserId.
    public static class UserEntity
    {
        [FunctionName(nameof(UserEntity))]
        public static Task HandleOperation(
            [EntityTrigger] IDurableEntityContext context)
        {
            var state = context.GetState(() => new UserStatus()
            {
                UserId = context.Key,
            });

            switch (context.OperationName)
            {
                case "set-location":
                    {
                        if (state.CurrentRide != null)
                        {
                            throw new InvalidOperationException("currently doing a ride - must complete current ride before looking for another one");
                        }

                        var newLocation = context.GetInput<int?>();

                        // update the location / whether this user is looking for a match
                        state.UpdateLocation(newLocation, context);
                    }
                    break;

                case "set-ride":
                    {
                        state.CurrentRide = context.GetInput<RideInfo>();

                        // set location to null because this user
                        // is no longer looking for a match
                        state.UpdateLocation(null, context);
                    }
                    break;

                case "clear-ride":
                    {
                        var rideId = context.GetInput<Guid>();

                        if (state.CurrentRide != null
                            && state.CurrentRide.RideId == rideId)
                        {
                            if (context.Key == state.CurrentRide.DriverId)
                            {
                                // forward signal to rider
                                context.SignalEntity(
                                    new EntityId(nameof(UserEntity), state.CurrentRide.RiderId),
                                    "clear-ride",
                                    rideId);
                            }

                            state.CurrentRide = null;
                        }
                    }
                    break;

                case "get":
                    {
                        context.Return(state);
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }
}