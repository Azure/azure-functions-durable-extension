// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace RideSharing
{
    // The RegionEntity entity tracks riders and drivers that are 
    // looking for a match in a particular region. 
    // There is one RegionEntity per region. 
    // The entity key is the zipcode of the region.

    // The presence/absence of users in a particular region 
    // may lag behind the actual, latest location of a user, but is eventually consistent:
    // user entities send add-user / remove-user signals whenever they change
    // their region.

    public static class RegionEntity
    {
        [FunctionName(nameof(RegionEntity))]
        public static Task HandleOperation(
        [EntityTrigger] IDurableEntityContext context)
        {
            var state = context.GetState(() => new HashSet<string>());

            switch (context.OperationName)
            {
                case "add-user":
                    {
                        var userId = context.GetInput<string>();
                        state.Add(userId);
                    }
                    break;

                case "remove-user":
                    {
                        var driverId = context.GetInput<string>();
                        state.Remove(driverId);
                    }
                    break;

                case "get-available-drivers":
                    {
                        context.Return(state.Where(id => id.StartsWith("D")).ToArray());
                    }
                    break;

                case "get-available-riders":
                    {
                        context.Return(state.Where(id => id.StartsWith("R")).ToArray());
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }
}