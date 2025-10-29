
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    public class DurableTaskScaleExtension
    {
        private readonly IDurabilityProviderFactory durabilityProviderFactory;
        private readonly DurabilityProvider defaultDurabilityProvider;
        private readonly DurableTaskOptions options;
		private readonly ILogger logger;
        private readonly IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskScaleExtension"/> class.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="durabilityProviderFactories"></param>
        public DurableTaskScaleExtension(
		    DurableTaskOptions options,
		    ILogger logger,
			IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories)
		{
				this.options = options ?? throw new ArgumentNullException(nameof(options));
				this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
				this.durabilityProviderFactories = durabilityProviderFactories ?? throw new ArgumentNullException(nameof(durabilityProviderFactories));

				this.durabilityProviderFactory = GetDurabilityProviderFactory(this.options, this.logger, this.durabilityProviderFactories);
				this.defaultDurabilityProvider = this.durabilityProviderFactory.GetDurabilityProvider();
			}

		public IDurabilityProviderFactory DurabilityProviderFactory => this.durabilityProviderFactory;
		public DurabilityProvider DefaultDurabilityProvider => this.defaultDurabilityProvider;

		   private static IDurabilityProviderFactory GetDurabilityProviderFactory(
			   DurableTaskOptions options,
			   ILogger logger,
			   IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories)
		{
			const string DefaultProvider = "AzureStorage";
			   bool storageTypeIsConfigured = options.StorageProvider.TryGetValue("type", out object storageType);

			   if (!storageTypeIsConfigured)
			   {
				   try
				   {
					   IDurabilityProviderFactory defaultFactory = durabilityProviderFactories.First(f => f.Name.Equals(DefaultProvider));
					   logger.LogInformation($"Using the default storage provider: {DefaultProvider}.");
					   return defaultFactory;
				   }
				   catch (InvalidOperationException e)
				   {
					   throw new InvalidOperationException($"Couldn't find the default storage provider: {DefaultProvider}.", e);
				   }
			   }

			   try
			   {
				   IDurabilityProviderFactory selectedFactory = durabilityProviderFactories.First(f => string.Equals(f.Name, storageType.ToString(), StringComparison.OrdinalIgnoreCase));
				   logger.LogInformation($"Using the {storageType} storage provider.");
				   return selectedFactory;
			   }
			   catch (InvalidOperationException e)
			   {
				   IList<string> factoryNames = durabilityProviderFactories.Select(f => f.Name).ToList();
				   throw new InvalidOperationException($"Storage provider type ({storageType}) was not found. Available storage providers: {string.Join(", ", factoryNames)}.", e);
			   }
		}
	}
}
