// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Durable Task options for the in-memory emulator flavored Durable Task extension.
    /// </summary>
    public class DurableTaskEmulatorOptions : DurableTaskOptions
    {
        /// <summary>
        /// Section of configuration for the in-memory emulator provider.
        /// </summary>
        public EmulatorStorageOptions Emulator { get; } = new EmulatorStorageOptions();

        /// <inheritdoc />
        public override IStorageOptions StorageOptions => this.Emulator;
    }
}
