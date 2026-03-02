// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Simple test implementation of IWebJobsBuilder that wraps a ServiceCollection.
    /// This allows us to test AddDurableTask() without needing a full HostBuilder.
    /// </summary>
    internal class TestWebJobsBuilder : IWebJobsBuilder
    {
        public TestWebJobsBuilder(IServiceCollection services)
        {
            this.Services = services;
        }

        public IServiceCollection Services { get; }

        public IWebJobsBuilder AddExtension<TExtension>()
            where TExtension : class, IExtensionConfigProvider
        {
            this.Services.AddSingleton<IExtensionConfigProvider, TExtension>();
            return this;
        }
    }
}
