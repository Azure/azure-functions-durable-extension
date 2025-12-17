// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface implemented by DurabilityProviderFactories that are aware of the client that references them.
    /// </summary>
    public interface IExtensionAwareDurabilityProviderFactory
    {
        /// <summary>
        /// Configures the factory with a reference to the DurableTaskExtension client. Allows access to client properties when constructing the durability provider.
        /// </summary>
        /// <param name="config">The DurableTaskExtension client that uses this factory to produce DurabilityProviders.</param>
        void ConfigureWithDurableExtension(DurableTaskExtension config);
    }
}
