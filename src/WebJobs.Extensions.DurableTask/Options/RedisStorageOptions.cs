// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the Redis storage provider.
    /// </summary>
    public class RedisStorageOptions : CommonStorageProviderOptions
    {
        internal override void AddToDebugString(StringBuilder builder)
        {
            builder.Append(nameof(this.ConnectionStringName)).Append(": ").Append(this.ConnectionStringName);
        }

        internal override void Validate()
        {
            if (string.IsNullOrEmpty(this.ConnectionStringName))
            {
                throw new InvalidOperationException($"{nameof(RedisStorageOptions.ConnectionStringName)} must be populated to use the Redis storage provider");
            }
        }

        internal override void ValidateHubName(string hubName)
        {
        }
    }
}
