// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    // TODO: Use IConfigurationExtensions in WebJobs SDK when exposed
    internal static class ConfigurationExtensions
    {
#if !FUNCTIONS_V1
        public static IConfigurationSection GetWebJobsConnectionSection(this IConfiguration configuration, string connectionName)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Attempt to find the section with the prefix first
            string prefixedConnectionStringName = IConfigurationExtensions.GetPrefixedConnectionStringName(connectionName);
            IConfigurationSection section = GetConnectionStringOrSettingSection(configuration, prefixedConnectionStringName);

            // Otherwise look for the name directly
            return section.Exists() ? section : GetConnectionStringOrSettingSection(configuration, connectionName);
        }

        private static IConfigurationSection GetConnectionStringOrSettingSection(this IConfiguration configuration, string connectionName)
        {
            IConfigurationSection section = configuration?.GetSection("ConnectionStrings").GetSection(connectionName);
            return section.Exists() ? section : configuration?.GetSection(connectionName);
        }
#endif
    }
}
