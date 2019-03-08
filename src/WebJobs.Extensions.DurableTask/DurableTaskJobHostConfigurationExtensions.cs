// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Extension for registering a Durable Functions configuration with <c>JobHostConfiguration</c>.
    /// </summary>
    public static class DurableTaskJobHostConfigurationExtensions
    {
        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<DurableTaskExtension>()
                .BindOptions<DurableTaskOptions>()
                .Services.AddSingleton<IConnectionStringResolver, WebJobsConnectionStringProvider>();

            return builder;
        }

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="options">The configuration options for this extension.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskOptions> options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.AddDurableTask();
            builder.Services.AddSingleton(options);
            return builder;
        }

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{DurableTaskOptions}"/> to configure the provided <see cref="DurableTaskOptions"/>.</param>
        /// <returns>Returns the modified <paramref name="builder"/> object.</returns>
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder, Action<DurableTaskOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddDurableTask();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
