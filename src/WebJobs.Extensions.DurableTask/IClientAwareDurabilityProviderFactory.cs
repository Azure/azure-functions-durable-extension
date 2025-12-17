using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface implemented by DurabilityProviderFactories that are aware of the client that references them.
    /// </summary>
    internal interface IClientAwareDurabilityProviderFactory
    {
        /// <summary>
        /// Configures the factory with a reference to the DurableTaskExtension client. Allows access to client properties when constructing the durability provider.
        /// </summary>
        /// <param name="client">The DurableTaskExtension client that uses this factory to produce DurabilityProviders.</param>
        void ConfigureWithDurableClient(DurableTaskExtension client);
    }
}
