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
        public T Resolve<T>(string name)
            where T : class
        {
            string prefixedName = IConfigurationExtensions.GetPrefixedConnectionStringName(name);
            T options = this.hostConfiguration.GetSection(prefixedName).Get<T>();
            return options ?? this.hostConfiguration.GetSection(name).Get<T>();
        }

        /// <inheritdoc />
        public string Resolve(string connectionStringName)
        {
            return this.hostConfiguration.GetWebJobsConnectionString(connectionStringName);
        }
#else
        /// <inheritdoc />
        public T Resolve<T>(string name)
            where T : class
        {
            throw new NotSupportedException("Only connection strings are supported");
        }

        /// <inheritdoc />
        public string Resolve(string connectionStringName)
        {
            return Microsoft.Azure.WebJobs.Host.AmbientConnectionStringProvider.Instance.GetConnectionString(connectionStringName);
        }
#endif
    }
}
