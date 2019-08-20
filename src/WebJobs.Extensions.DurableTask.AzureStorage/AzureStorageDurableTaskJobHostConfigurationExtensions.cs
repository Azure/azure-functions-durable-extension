// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Azure;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
#if NETSTANDARD2_0
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
#else
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Extension for registering a Durable Functions configuration with <c>JobHostConfiguration</c>.
    /// </summary>
    public static class AzureStorageDurableTaskJobHostConfigurationExtensions
    {
#if NETSTANDARD2_0
        /// <summary>
        /// Adds the Azure STorage Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddAzureStorageDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<DurableTaskExtensionAzureStorageConfig>()
                .BindOptions<DurableTaskAzureStorageOptions>()
                .Services.AddSingleton<IOrchestrationServiceFactory, AzureStorageOrchestrationServiceFactory>()
                         .AddCoreDurableTaskServices();

            return builder;
        }

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="options">The configuration options for this extension.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddAzureStorageDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskAzureStorageOptions> options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.AddAzureStorageDurableTask();
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
            DurableTaskExtensionAzureStorageConfig listenerConfig)
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
