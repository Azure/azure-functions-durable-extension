using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Chirper.Service
{
    // The UserFollows entity stores all the follows of ONE user.
    // The entity key is the userId.
    public static class UserFollows
    {
        public enum Ops
        {
            Add,
            Remove,
            Get,
        }

        [FunctionName(nameof(UserFollows))]
        public static Task HandleOperation(
            [EntityTrigger] IDurableEntityContext context)
        {   
            var follows = context.GetState(() => new List<string>());

            switch (Enum.Parse<Ops>(context.OperationName))
            {
                case Ops.Add:
                    {
                        var userId = context.GetInput<string>();

                        follows.Add(userId);
                    }
                    break;

                case Ops.Remove:
                    {
                        var userId = context.GetInput<string>();

                        follows.Remove(userId);
                    }
                    break;

                case Ops.Get:
                    {
                        context.Return(follows);
                    }
                    break;

            }

            return Task.CompletedTask;
        }
    }
}