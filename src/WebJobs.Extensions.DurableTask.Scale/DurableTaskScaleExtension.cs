
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    public class DurableTaskScaleExtension : IExtensionConfigProvider
    {
        private readonly IScalabilityProviderFactory scalabilityProviderFactory;
        private readonly ScalabilityProvider defaultscalabilityProvider;
        private readonly DurableTaskScaleOptions options;
		private readonly ILogger logger;
        private readonly IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskScaleExtension"/> class.
        /// </summary>
        /// <param name="options">The options for the Durable Task Scale Extension.</param>
        /// <param name="logger"></param>
        /// <param name="scalabilityProviderFactories"></param>
        public DurableTaskScaleExtension(
		    DurableTaskScaleOptions options,
		    ILogger logger,
            IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories)
		{
				this.options = options ?? throw new ArgumentNullException(nameof(options));
				this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
                this.scalabilityProviderFactories = scalabilityProviderFactories ?? throw new ArgumentNullException(nameof(scalabilityProviderFactories));

                this.scalabilityProviderFactory = GetScalabilityProviderFactory(this.options, this.logger, this.scalabilityProviderFactories);
				this.defaultscalabilityProvider = this.scalabilityProviderFactory.GetDurabilityProvider();
			}

        public IScalabilityProviderFactory ScalabilityProviderFactory => this.scalabilityProviderFactory;
        public ScalabilityProvider DefaultScalabilityProvider => this.defaultscalabilityProvider;

        public void Initialize(ExtensionConfigContext context)
        {
            // Extension initialization - no-op for scale package
        }

        internal static IScalabilityProviderFactory GetScalabilityProviderFactory(
			   DurableTaskScaleOptions options,
			   ILogger logger,
               IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories)
		{
			const string DefaultProvider = "AzureStorage";
			   bool storageTypeIsConfigured = options.StorageProvider.TryGetValue("type", out object storageType);

			   if (!storageTypeIsConfigured)
			   {
				   try
				   {
                       IScalabilityProviderFactory defaultFactory = scalabilityProviderFactories.First(f => f.Name.Equals(DefaultProvider));
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
                   IScalabilityProviderFactory selectedFactory = scalabilityProviderFactories.First(f => string.Equals(f.Name, storageType.ToString(), StringComparison.OrdinalIgnoreCase));
				   logger.LogInformation($"Using the {storageType} storage provider.");
				   return selectedFactory;
			   }
			   catch (InvalidOperationException e)
			   {
				   IList<string> factoryNames = scalabilityProviderFactories.Select(f => f.Name).ToList();
				   throw new InvalidOperationException($"Storage provider type ({storageType}) was not found. Available storage providers: {string.Join(", ", factoryNames)}.", e);
			   }
		}
	}
}
