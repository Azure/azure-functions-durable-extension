// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests for <see cref="IDurabilityProviderFactory.SetRegisteredFunctions"/>.
    /// </summary>
    public class SetRegisteredFunctionsTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultInterfaceMethod_IsNoOp()
        {
            // A factory that does NOT override SetRegisteredFunctions should use
            // the default no-op implementation without throwing.
            IDurabilityProviderFactory factory = new NoOpDurabilityProviderFactory();

            factory.SetRegisteredFunctions(
                new[] { "Orch1", "Orch2" },
                new[] { "Activity1" },
                new[] { "Entity1" });

            // No exception = pass. The default method body is empty.
        }

        /// <summary>
        /// A minimal factory that does NOT override SetRegisteredFunctions,
        /// exercising the default interface method.
        /// </summary>
        private class NoOpDurabilityProviderFactory : IDurabilityProviderFactory
        {
            public string Name => "NoOpProvider";

            public DurabilityProvider GetDurabilityProvider()
                => throw new System.NotImplementedException();

            public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
                => throw new System.NotImplementedException();
        }
    }
}
