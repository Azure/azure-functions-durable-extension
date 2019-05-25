// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;

namespace RideSharing
{
    // The UserEntity tracks the current state of a particular user (driver or rider). 
    // There is one UserEntity entity per user. 
    // The entity key is the UserId.
    [JsonObject(MemberSerialization.OptIn)]
    public class UserEntity
    {
        // the unique id for this user
        [JsonProperty]
        public string UserId;

        // the currently advertised location (as a zipcode), if the driver/rider is
        // looking for a rider/driver, or null otherwise.
        [JsonProperty]
        public int? Location { get; private set; }

        // the ride to which this driver/rider has been assigned, or null
        // if not assigned.
        [JsonProperty]
        public RideInfo CurrentRide { get; set; }

        // Boilerplate (entry point for the functions runtime)
        [FunctionName(nameof(UserEntity))]
        public static Task HandleEntityOperation([EntityTrigger] IDurableEntityContext context)
        {
            // we initialize the entity explicitly here (instead of relying on the implicit default constructor)
            // so we can set the user id to match the entity key
            if (context.IsNewlyConstructed)
            {
                context.SetState(new UserEntity()
                {
                    UserId = context.EntityKey,
                });
            }

            return context.DispatchAsync<UserEntity>();
        }

        // get the current state of this user
        public UserEntity Get()
        {
            return this;
        }

        // update the location / whether this user is looking for a match
        public void SetLocation(int? newLocation)
        {
            if (CurrentRide != null)
            {
                throw new InvalidOperationException("currently doing a ride - must complete current ride first");
            }

            this.UpdateLocationAndNotify(newLocation);
        }

        // update the location / whether this user is looking for a match
        public void SetRide(RideInfo rideInfo)
        {
            CurrentRide = rideInfo;

            // this user is no longer looking for a match - clear location
            this.UpdateLocationAndNotify(null);
        }

        // update the location / whether this user is looking for a match
        public void ClearRide(Guid rideId)
        {
            if (CurrentRide != null
                && CurrentRide.RideId == rideId)
            {
                if (Entity.Current.EntityKey == CurrentRide.DriverId)
                {
                    // forward signal to rider
                    Entity.Current.SignalEntity(
                        new EntityId(nameof(UserEntity), CurrentRide.RiderId),
                        nameof(UserEntity.ClearRide),
                        rideId);
                }

                CurrentRide = null;
            }
        }

        private void UpdateLocationAndNotify(int? newLocation)
        {
            if (this.Location != newLocation)
            {
                // un-advertise availability from current location
                this.AdvertiseAvailability(false);

                // udpate the location
                this.Location = newLocation;

                // advertise availability for new location
                this.AdvertiseAvailability(true);
            }
        }

        private void AdvertiseAvailability(bool available)
        {
            if (this.Location != null)
            {
                Entity.Current.SignalEntity(
                    new EntityId(nameof(RegionEntity), this.Location.Value.ToString()),
                    available ? nameof(RegionEntity.AddUser) : nameof(RegionEntity.RemoveUser),
                    this.UserId);
            }
        }

    }
}