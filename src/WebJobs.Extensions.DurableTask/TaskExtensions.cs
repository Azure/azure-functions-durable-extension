// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class TaskExtensions
    {
        public static T EnsureCompleted<T>(this Task<T> task) =>
            task.GetAwaiter().GetResult();
    }
}
