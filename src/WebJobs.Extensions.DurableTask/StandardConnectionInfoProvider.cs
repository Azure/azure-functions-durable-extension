// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Connection info provider which resolves connection information from a standard application (Non WebJob).
    /// </summary>
    internal class StandardConnectionInfoProvider : IConnectionInfoResolver
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

        /// <inheritdoc />
        public T Resolve<T>(string name)
            where T : class
        {
#if FUNCTIONS_V1
            throw new NotSupportedException("Only connection strings are supported");
#else
            return this.configuration.GetSection(name).Get<T>();
#endif
        }

        /// <inheritdoc />
        public string Resolve(string connectionStringName)
        {
            return this.configuration[connectionStringName];
        }
    }
}