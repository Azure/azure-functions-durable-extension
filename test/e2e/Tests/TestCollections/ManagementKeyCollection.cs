// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[CollectionDefinition(Name, DisableParallelization = true)]
public class ManagementKeyCollection : ICollectionFixture<ManagementKeyFixture>
{
    public const string Name = "ManagementKeyTests";
}
