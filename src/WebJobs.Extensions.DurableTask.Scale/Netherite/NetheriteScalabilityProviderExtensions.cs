// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Netherite
{
    /// <summary>
    /// Extension methods for configuring the Netherite Durable Task backend.
    /// </summary>
    public static class NetheriteScalabilityProviderExtensions
    {
        /// <summary>
        /// Registers the Netherite Durable Task backend with the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to add the Netherite provider to.</param>
        public static void AddDurableTaskNetheriteBackend(this IServiceCollection services)
        {
            services.AddSingleton<IScalabilityProviderFactory, NetheriteScalabilityProviderFactory>();
        }
    }
}
