// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;

namespace RideSharing
{
    /// <summary>
    /// A data structure representing a ride, i.e. a matching of driver and rider.
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class RideInfo
    {
        public Guid RideId { get; set; }

        public string DriverId { get; set; }

        public string RiderId { get; set; }

        public int DriverLocation { get; set; }

        public int RiderLocation { get; set; }
    }
}
