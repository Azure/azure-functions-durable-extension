// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Xunit;

#pragma warning disable xUnit1025 // InlineData should be unique within the Theory it belongs to
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class GuidManagerTests
    {
        [Theory]
        [InlineData(GuidManager.DnsNamespaceValue)]
        [InlineData(GuidManager.UrlNamespaceValue)]
        [InlineData(GuidManager.IsoOidNamespaceValue)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Guid_With_Same_Namespace_And_Name_Are_Equal(string namespaceValue)
        {
            string name = "dccf029b5c4b4031a3b943f9f745e35b";
            Guid firstGuid = GuidManager.CreateDeterministicGuid(namespaceValue, name);
            Guid secondGuid = GuidManager.CreateDeterministicGuid(namespaceValue, name);
            Guid thirdGuid = GuidManager.CreateDeterministicGuid(namespaceValue, name);
            Assert.Equal(firstGuid, secondGuid);
            Assert.Equal(firstGuid, thirdGuid);
        }

        [Theory]
        [InlineData(GuidManager.DnsNamespaceValue)]
        [InlineData(GuidManager.UrlNamespaceValue)]
        [InlineData(GuidManager.IsoOidNamespaceValue)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Guid_With_Same_Namespace_And_Different_Name_Are_Not_Equal(string namespaceValue)
        {
            string firstName = "dccf029b5c4b4031a3b943f9f745e35b";
            string secondName = "Kccf029b5c4b4031a3b943f9f745e35b";
            Guid firstGuid = GuidManager.CreateDeterministicGuid(namespaceValue, firstName);
            Guid secondGuid = GuidManager.CreateDeterministicGuid(namespaceValue, secondName);
            Assert.NotEqual(firstGuid, secondGuid);
        }

        [Theory]
        [InlineData(GuidManager.DnsNamespaceValue, GuidManager.UrlNamespaceValue)]
        [InlineData(GuidManager.DnsNamespaceValue, GuidManager.IsoOidNamespaceValue)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Guid_With_Same_Name_And_Different_Namespace_Are_Not_Equal(string firstNamespaceValue, string secondNamespaceValue)
        {
            string name = "dccf029b5c4b4031a3b943f9f745e35b";
            Guid firstGuid = GuidManager.CreateDeterministicGuid(firstNamespaceValue, name);
            Guid secondGuid = GuidManager.CreateDeterministicGuid(secondNamespaceValue, name);
            Assert.NotEqual(firstGuid, secondGuid);
        }
    }
}
