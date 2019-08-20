// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETSTANDARD2_0
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Extension for registering a Durable Functions configuration with <c>JobHostConfiguration</c>.
    /// </summary>
    public static class DurableTaskJobHostConfigurationExtensions
    {
        /// <summary>
        /// Adds the core Durable Task extension services to the provided <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddCoreDurableTaskServices(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IConnectionStringResolver, WebJobsConnectionStringProvider>()
                    .AddSingleton<IDurableHttpMessageHandlerFactory, DurableHttpMessageHandlerFactory>();

            return services;
        }
    }
}
#endif