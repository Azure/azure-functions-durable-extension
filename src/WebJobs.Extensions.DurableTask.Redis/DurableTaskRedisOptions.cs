// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Durable Task options for the Redis flavored Durable Task extension.
    /// </summary>
    public class DurableTaskRedisOptions : DurableTaskOptions
    {
        /// <summary>
        /// Section of configuration for the in-memory emulator provider.
        /// </summary>
        public RedisStorageOptions RedisStorageProvider { get; set; } = new RedisStorageOptions();

        /// <inheritdoc />
        public override IStorageOptions StorageOptions => this.RedisStorageProvider;
    }
}
