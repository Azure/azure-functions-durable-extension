// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Connection string provider which resolves connection strings from the an standard application (Non WebJob).
    /// </summary>
    [Obsolete("Please use StandardConnectionInfoProvider instead.")]
    public class StandardConnectionStringProvider : IConnectionStringResolver
    {
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="StandardConnectionStringProvider"/> class.
        /// </summary>
        /// <param name="configuration">A <see cref="IConfiguration"/> object provided by the application host.</param>
        public StandardConnectionStringProvider(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Looks up a connection string value given a name.
        /// </summary>
        /// <param name="connectionStringName">The name of the connection string.</param>
        /// <returns>Returns the resolved connection string value.</returns>
        public string Resolve(string connectionStringName)
        {
            return this.configuration[connectionStringName];
        }
    }
}