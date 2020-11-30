// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations
{
    /// <summary>
    ///     Factory class to create Durable Client to start works outside an azure function context.
    /// </summary>
    public class DurableClientFactory : IDurableClientFactory, IDisposable
    {
        // Creating client objects is expensive, so we cache them when the attributes match.
        // Note that DurableClientAttribute defines a custom equality comparer.
        private readonly ConcurrentDictionary<DurableClientAttribute, DurableClient> cachedClients =
            new ConcurrentDictionary<DurableClientAttribute, DurableClient>();

        private readonly ConcurrentDictionary<DurableClientAttribute, HttpApiHandler> cachedHttpListeners =
            new ConcurrentDictionary<DurableClientAttribute, HttpApiHandler>();

        private readonly DurableClientOptions defaultDurableClientOptions;
        private readonly DurableTaskOptions durableTaskOptions;
        private readonly IDurabilityProviderFactory durabilityProviderFactory;
        private readonly ILogger logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DurableClientFactory"/> class.
        /// </summary>
        /// <param name="defaultDurableClientOptions">Default Options to Build Durable Clients.</param>
        /// <param name="orchestrationServiceFactory">The factory used to create orchestration service based on the configured storage provider.</param>
        /// <param name="loggerFactory">The logger factory used for extension-specific logging and orchestration tracking.</param>
        /// <param name="durableTaskOptions">The configuration options for this extension.</param>
        /// <param name="messageSerializerSettingsFactory">The factory used to create <see cref="JsonSerializerSettings"/> for message settings.</param>
        public DurableClientFactory(
            IOptions<DurableClientOptions> defaultDurableClientOptions,
            IOptions<DurableTaskOptions> durableTaskOptions,
            IDurabilityProviderFactory orchestrationServiceFactory,
            ILoggerFactory loggerFactory,
            IMessageSerializerSettingsFactory messageSerializerSettingsFactory = null)
        {
            this.logger = loggerFactory.CreateLogger(DurableTaskExtension.LoggerCategoryName);

            this.durabilityProviderFactory = orchestrationServiceFactory;
            this.defaultDurableClientOptions = defaultDurableClientOptions.Value;
            this.durableTaskOptions = durableTaskOptions?.Value ?? new DurableTaskOptions();

            this.MessageDataConverter = DurableTaskExtension.CreateMessageDataConverter(messageSerializerSettingsFactory);
            this.TraceHelper = new EndToEndTraceHelper(this.logger, this.durableTaskOptions.Tracing.TraceReplayEvents);
        }

        internal MessagePayloadDataConverter MessageDataConverter { get; private set; }

        internal EndToEndTraceHelper TraceHelper { get; private set; }

        /// <summary>
        /// Gets a <see cref="IDurableClient"/> using configuration from a <see cref="DurableClientOptions"/> instance.
        /// </summary>
        /// <param name="durableClientOptions">options containing the client configuration parameters.</param>
        /// <returns>Returns a <see cref="IDurableClient"/> instance. The returned instance may be a cached instance.</returns>
        public IDurableClient CreateClient(DurableClientOptions durableClientOptions)
        {
            if (durableClientOptions == null)
            {
                throw new ArgumentException("Please configure 'DurableClientOptions'");
            }

            if (string.IsNullOrWhiteSpace(durableClientOptions.TaskHub))
            {
                throw new ArgumentException("Please provide value for 'TaskHub'");
            }

            DurableClientAttribute attribute = new DurableClientAttribute(durableClientOptions);

            DurableClient client = this.cachedClients.GetOrAdd(
                attribute,
                attr =>
                {
                    DurabilityProvider innerClient = this.durabilityProviderFactory.GetDurabilityProvider(attribute);
                    return new DurableClient(innerClient, null, attribute, this.MessageDataConverter, this.TraceHelper, this.durableTaskOptions);
                });

            return client;
        }

        /// <summary>
        /// Gets a <see cref="IDurableClient"/> using configuration from a <see cref="DurableClientOptions"/> instance.
        /// </summary>
        /// <returns>Returns a <see cref="IDurableClient"/> instance. The returned instance may be a cached instance.</returns>
        public IDurableClient CreateClient()
        {
            return this.CreateClient(this.defaultDurableClientOptions);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var cachedHttpListener in this.cachedHttpListeners)
            {
                cachedHttpListener.Value?.Dispose();
            }
        }
    }
}