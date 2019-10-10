// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using WebJobs.Extensions.DurableTask.CustomStorage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Durable Task options for the in-memory emulator flavored Durable Task extension.
    /// </summary>
    public class DurableTaskCustomStorageOptions : DurableTaskOptions
    {
        /// <summary>
        /// Section of configuration for the in-memory emulator provider.
        /// </summary>
        public CustomStorageOptions Custom { get; } = new CustomStorageOptions();

        /// <inheritdoc />
        public override IStorageOptions StorageOptions => this.Custom;
    }
}
