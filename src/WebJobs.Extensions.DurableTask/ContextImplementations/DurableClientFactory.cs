// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations
{
    /// <summary>
    ///     Factory class to create Durable Client to start works outside an azure function context.
    /// </summary>
    public class DurableClientFactory : IDurableClientFactory
    {
        private readonly DurableTaskExtension durableTaskConfig;
        private readonly DurableClientOptions defaultDurableClientOptions;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DurableClientFactory"/> class.
        /// </summary>
        /// <param name="durableTaskConfig">Configuration for the Durable Functions extension.</param>
        /// <param name="defaultDurableClientOptions">Default Options to Build Durable Clients.</param>
        public DurableClientFactory(DurableTaskExtension durableTaskConfig, IOptions<DurableClientOptions> defaultDurableClientOptions)
        {
            this.durableTaskConfig = durableTaskConfig;
            this.defaultDurableClientOptions = defaultDurableClientOptions.Value;
        }

        /// <summary>
        /// Gets a <see cref="IDurableClient"/> using configuration from a <see cref="DurableClientOptions"/> instance.
        /// </summary>
        /// <param name="durableClientOptions">options containing the client configuration parameters.</param>
        /// <returns>Returns a <see cref="IDurableClient"/> instance. The returned instance may be a cached instance.</returns>
        public IDurableClient CreateClient(DurableClientOptions durableClientOptions)
        {
            if (string.IsNullOrWhiteSpace(durableClientOptions.TaskHub))
            {
                durableClientOptions.TaskHub = DurableTaskOptions.DefaultHubName;
            }

            return this.durableTaskConfig.GetClient(new DurableClientAttribute(durableClientOptions));
        }

        /// <summary>
        /// Gets a <see cref="IDurableClient"/> using configuration from a <see cref="DurableClientOptions"/> instance.
        /// </summary>
        /// <returns>Returns a <see cref="IDurableClient"/> instance. The returned instance may be a cached instance.</returns>
        public IDurableClient CreateClient()
        {
            return this.CreateClient(this.defaultDurableClientOptions);
        }
    }
}