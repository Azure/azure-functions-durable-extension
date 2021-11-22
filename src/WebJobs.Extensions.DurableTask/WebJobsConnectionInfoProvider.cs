// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Connection info provider which resolves connection information from the WebJobs context.
    /// </summary>
    internal class WebJobsConnectionInfoProvider : IConnectionInfoResolver
    {
#if !FUNCTIONS_V1
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
            return this.hostConfiguration.GetWebJobsConnectionSection(name);
        }
#else
        /// <inheritdoc />
        public IConfigurationSection Resolve(string name)
        {
            // The returned key doesn't reflect whether the ConnectionString section was used or if the name was ultimately prefixed.
            return new ReadOnlyConfigurationValue(name, Host.AmbientConnectionStringProvider.Instance.GetConnectionString(name));
        }
#endif
    }
}
