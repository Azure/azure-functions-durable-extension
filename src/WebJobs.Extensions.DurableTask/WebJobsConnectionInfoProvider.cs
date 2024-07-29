// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Connection info provider which resolves connection information from the WebJobs context.
    /// </summary>
    public class WebJobsConnectionInfoProvider : IConnectionInfoResolver
    {
        private readonly IConfiguration hostConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebJobsConnectionInfoProvider"/> class.
        /// </summary>
        /// <param name="hostConfiguration">A <see cref="IConfiguration"/> object provided by the WebJobs host.</param>
        public WebJobsConnectionInfoProvider(IConfiguration hostConfiguration)
        {
            this.hostConfiguration = hostConfiguration ?? throw new ArgumentNullException(nameof(hostConfiguration));
        }

        /// <inheritdoc />
        public IConfigurationSection Resolve(string name)
        {
            // The below represents the implementation of this.hostConfiguration.GetWebJobsConnectionSection(name), defined in the WebJobs SDK
            // but not available to Functions v3 at runtime.
            // Source: https://github.com/Azure/azure-webjobs-sdk/blob/b6d5b52da5d2fb457efbf359cbdd733186aacf7c/src/Microsoft.Azure.WebJobs.Host/Extensions/IConfigurationExtensions.cs#L103-L133
            static IConfigurationSection GetConnectionStringOrSettingSection(IConfiguration configuration, string connectionName)
            {
                IConfigurationSection connectionStringSection = configuration?.GetSection("ConnectionStrings").GetSection(connectionName);

                if (connectionStringSection.Exists())
                {
                    return connectionStringSection;
                }

                return configuration?.GetSection(connectionName);
            }

            string prefixedConnectionStringName = IConfigurationExtensions.GetPrefixedConnectionStringName(name);
            IConfigurationSection section = GetConnectionStringOrSettingSection(this.hostConfiguration, prefixedConnectionStringName);

            if (!section.Exists())
            {
                // next try a direct unprefixed lookup
                section = GetConnectionStringOrSettingSection(this.hostConfiguration, name);
            }

            return section;
        }
    }
}
