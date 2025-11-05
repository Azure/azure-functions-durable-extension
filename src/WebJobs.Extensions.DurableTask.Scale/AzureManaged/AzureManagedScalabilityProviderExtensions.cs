// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged
{
    /// <summary>
    /// Extension methods for configuring the Azure Managed Durable Task backend.
    /// </summary>
    public static class AzureManagedScalabilityProviderExtensions
    {
        /// <summary>
        /// Registers the Azure Managed Durable Task backend with the dependency injection container.
        /// </summary>
        public static void AddDurableTaskManagedBackend(this IServiceCollection services)
        {
            services.AddSingleton<IScalabilityProviderFactory, AzureManagedScalabilityProviderFactory>();
        }
    }
}
