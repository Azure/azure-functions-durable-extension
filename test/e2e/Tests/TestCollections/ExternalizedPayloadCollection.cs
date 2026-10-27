// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[CollectionDefinition(Name, DisableParallelization = true)]
public class ExternalizedPayloadCollection : ICollectionFixture<ExternalizedPayloadFixture>
{
    public const string Name = "ExternalizedPayloadTests";
}
