// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;

namespace RideSharing
{ 
    /// <summary>
    /// A data structure representing a ride, i.e. a matching of driver and rider.
    /// </summary>
    public class RideInfo
    {
        [JsonProperty("rideId")]
        public Guid RideId { get; set; }

        [JsonProperty("driverId")]
        public string DriverId { get; set; }

        [JsonProperty("riderId")]
        public string RiderId { get; set; }

        [JsonProperty("driverLocation")]
        public int DriverLocation { get; set; }

        [JsonProperty("riderLocation")]
        public int RiderLocation { get; set; }
    }
}
