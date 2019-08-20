// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
#if NETSTANDARD2_0
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
#else
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Extension for registering a Durable Functions configuration with <c>JobHostConfiguration</c>.
    /// </summary>
    public static class RedisDurableTaskJobHostConfigurationExtensions
    {
#if NETSTANDARD2_0
        /// <summary>
        /// Adds the Azure STorage Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddRedisDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<DurableTaskExtensionRedisConfig>()
                .BindOptions<DurableTaskRedisOptions>()
                .Services.AddSingleton<IOrchestrationServiceFactory, RedisOrchestrationServiceFactory>()
                         .AddCoreDurableTaskServices();

            return builder;
        }

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="options">The configuration options for this extension.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddRedisDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskRedisOptions> options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.AddRedisDurableTask();
            builder.Services.AddSingleton(options);
            return builder;
        }

#else
        /// <summary>
        /// Enable running durable orchestrations implemented as functions.
        /// </summary>
        /// <param name="hostConfig">Configuration settings of the current <c>JobHost</c> instance.</param>
        /// <param name="listenerConfig">Durable Functions configuration.</param>
        public static void Use(
            this JobHostConfiguration hostConfig,
            DurableTaskExtensionRedisConfig listenerConfig)
        {
            if (hostConfig == null)
            {
                throw new ArgumentNullException(nameof(hostConfig));
            }

            if (listenerConfig == null)
            {
                throw new ArgumentNullException(nameof(listenerConfig));
            }

            IExtensionRegistry extensions = hostConfig.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(listenerConfig);
        }
#endif
    }
}
