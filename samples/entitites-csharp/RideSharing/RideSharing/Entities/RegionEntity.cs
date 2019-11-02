// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;

namespace RideSharing
{
    // The RegionEntity entity tracks riders and drivers that are 
    // looking for a match in a particular region. 
    // There is one RegionEntity per region. 
    // The entity key is the zipcode of the region.
    //
    // The presence/absence of users in a particular region 
    // may lag behind the actual, latest location of those users, but is eventually consistent:
    // the user entities send add-user / remove-user signals whenever they change
    // their region.
    [JsonObject(MemberSerialization.OptIn)]
    public class RegionEntity : IRegionEntity
    {
        [JsonProperty]
        public HashSet<string> Users { get; set; } = new HashSet<string>();

        // Boilerplate (entry point for the functions runtime)
        [FunctionName(nameof(RegionEntity))]
        public static Task HandleEntityOperation([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<RegionEntity>();
        }

        public void AddUser(string user)
        {
            Users.Add(user);
        }

        public void RemoveUser(string user)
        {
            Users.Remove(user);
        }

        public Task<string[]> GetAvailableDrivers()
        {
            return Task.FromResult(Users.Where(id => id.StartsWith("D")).ToArray());
        }

        public Task<string[]> GetAvailableRiders()
        {
            return Task.FromResult(Users.Where(id => id.StartsWith("R")).ToArray());
        }

    }
}