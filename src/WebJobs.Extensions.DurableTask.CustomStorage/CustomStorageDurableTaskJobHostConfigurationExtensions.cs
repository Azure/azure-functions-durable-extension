// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WebJobs.Extensions.DurableTask.CustomStorage
{
    internal static class CustomStorageDurableTaskJobHostConfigurationExtensions
    {
        /// <summary>
        /// Adds the Azure STorage Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddCustomStorageDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<DurableTaskExtensionCustomStorageConfig>()
                .BindOptions<CustomStorageOptions>()
                .Services
                .AddCoreDurableTaskServices();

            return builder;
        }

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="options">The configuration options for this extension.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddCustomStorageDurableTask(this IWebJobsBuilder builder, IOptions<CustomStorageOptions> options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.AddCustomStorageDurableTask();
            builder.Services.AddSingleton(options);
            return builder;
        }
    }
}
