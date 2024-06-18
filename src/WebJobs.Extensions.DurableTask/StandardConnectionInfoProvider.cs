// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Connection info provider which resolves connection information from a standard application (Non WebJob).
    /// </summary>
    public class StandardConnectionInfoProvider : IConnectionInfoResolver
    {
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="StandardConnectionInfoProvider"/> class.
        /// </summary>
        /// <param name="configuration">A <see cref="IConfiguration"/> object provided by the application host.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
        public StandardConnectionInfoProvider(IConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        // This implementation is IConfigurationSection.Exists. Details: https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.Abstractions/src/ConfigurationExtensions.cs#L78
        // Functions host v1 (.net462 framework) doesn't support this method so we implement a substitute one here.
        private bool IfExists(IConfigurationSection section)
        {
            if (section == null)
            {
                return false;
            }

            if (section.Value == null)
            {
                return section.GetChildren().Any();
            }

            return true;
        }

        /// <inheritdoc />
        public IConfigurationSection Resolve(string name)
        {
            string prefixedConnectionStringName = "AzureWebJobs" + name;
            IConfigurationSection section = this.configuration?.GetSection(prefixedConnectionStringName);

            if (!this.IfExists(section))
            {
                // next try a direct unprefixed lookup
                section = this.configuration?.GetSection(name);
            }

            return section;
        }
    }
}
