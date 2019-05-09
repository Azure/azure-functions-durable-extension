// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;

namespace RideSharing
{
    // tracks the state of a user (driver or rider)
    public class UserStatus
    {
        // the unique id for this user
        [JsonProperty(PropertyName = "userId")]
        public string UserId;

        // the currently advertised location (as a zipcode), if the driver/rider is
        // looking for a rider/driver, or null otherwise.
        [JsonProperty(PropertyName = "location")]
        public int? Location { get; private set; }

        // the ride to which this driver/rider has been assigned, or null
        // if not assigned.
        [JsonProperty(PropertyName = "currentRide")]
        public RideInfo CurrentRide { get; set; }

        // updates the location, and sends signals to the regions to keep them informed
        public void UpdateLocation(int? newLocation, IDurableEntityContext context)
        {
            if (this.Location != newLocation)
            {
                // un-advertise availability from current location
                this.AdvertiseAvailability(false, context);

                // udpate the location
                this.Location = newLocation;

                // advertise availability for new location
                this.AdvertiseAvailability(true, context);
            }
        }

        protected void AdvertiseAvailability(bool available, IDurableEntityContext context)
        {
            if (this.Location != null)
            {
                context.SignalEntity(
                        new EntityId(nameof(RegionEntity), this.Location.Value.ToString()),
                        available ? "add-user" : "remove-user",
                        this.UserId);
            }
        }
    }


}
