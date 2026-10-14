// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;

#pragma warning disable xUnit1025 // InlineData should be unique within the Theory it belongs to
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class GuidManagerTests
    {
        private const string ArbitraryNamespaceValue = "{00112233-4455-6677-8899-AABBCCDDEEFF}";
        private const int Version3 = (int)GuidManager.DeterministicGuidVersion.V3;
        private const int Version5 = (int)GuidManager.DeterministicGuidVersion.V5;

        [Theory]
        [InlineData(GuidManager.DnsNamespaceValue, "durable-orchestration", Version3, "88d69f57-f159-341d-9480-c426fac9a13e")]
        [InlineData(GuidManager.DnsNamespaceValue, "durable-orchestration", Version5, "6d46c360-942e-5558-8c07-d342dff008f0")]
        [InlineData(GuidManager.UrlNamespaceValue, "durable-orchestration", Version3, "c4b8d656-a124-3e7c-9b3d-af865b7796d5")]
        [InlineData(GuidManager.UrlNamespaceValue, "durable-orchestration", Version5, "6f29a180-0cf5-57ec-8693-6040fb49c731")]
        [InlineData(GuidManager.IsoOidNamespaceValue, "durable-orchestration", Version3, "c1f7db5c-d329-33e4-87f3-48c8890ab69b")]
        [InlineData(GuidManager.IsoOidNamespaceValue, "durable-orchestration", Version5, "ee4f9759-7b7e-5e72-83c7-a57ede615128")]
        [InlineData(ArbitraryNamespaceValue, "instance_2026-07-27T20:30:10.3440000Z_42", Version3, "06c3aff0-9ad6-3592-a23a-880db62f1653")]
        [InlineData(ArbitraryNamespaceValue, "instance_2026-07-27T20:30:10.3440000Z_42", Version5, "ad300818-c0c4-54d1-b99b-f12d2ee182a5")]
        [InlineData(ArbitraryNamespaceValue, "café/東京/🚀", Version3, "d5242313-770a-3c00-845c-dd8bc3ea4b34")]
        [InlineData(ArbitraryNamespaceValue, "café/東京/🚀", Version5, "5ed369f7-9646-51cb-be03-6cf98db5fb83")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateDeterministicGuid_MatchesGoldenVector(
            string namespaceValue,
            string name,
            int versionValue,
            string expected)
        {
            var version = (GuidManager.DeterministicGuidVersion)versionValue;
            Guid actual = GuidManager.CreateDeterministicGuid(namespaceValue, name, version);

            Assert.Equal(new Guid(expected), actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateDeterministicGuid_DefaultsToVersion5()
        {
            Guid actual = GuidManager.CreateDeterministicGuid(
                ArbitraryNamespaceValue,
                "instance_2026-07-27T20:30:10.3440000Z_42");

            Assert.Equal(new Guid("ad300818-c0c4-54d1-b99b-f12d2ee182a5"), actual);
        }

        [Theory]
        [InlineData(Version3, "420d27c1-f527-3646-930d-16638a2c49d5")]
        [InlineData(Version5, "3a99fafb-17c3-5efd-a656-06a7a55fb83a")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateDeterministicGuid_MatchesGoldenVector_ForLongName(
            int versionValue,
            string expected)
        {
            string name = new string('x', 1024) + "終";
            var version = (GuidManager.DeterministicGuidVersion)versionValue;

            Guid actual = GuidManager.CreateDeterministicGuid(ArbitraryNamespaceValue, name, version);

            Assert.Equal(new Guid(expected), actual);
        }

        [Theory]
        [InlineData(null, "name")]
        [InlineData("", "name")]
        [InlineData("not-a-guid", "name")]
        [InlineData(" ", "name")]
        [InlineData(GuidManager.UrlNamespaceValue, null)]
        [InlineData(GuidManager.UrlNamespaceValue, "")]
        [InlineData("not-a-guid", null)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateDeterministicGuid_PreservesInvalidInputExceptions(string namespaceValue, string name)
        {
            Exception expected = Record.Exception(
                () => CreateReferenceDeterministicGuid(namespaceValue, name, GuidManager.DeterministicGuidVersion.V5));
            Exception actual = Record.Exception(
                () => GuidManager.CreateDeterministicGuid(namespaceValue, name, GuidManager.DeterministicGuidVersion.V5));

            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            Assert.Equal((expected as ArgumentException)?.ParamName, (actual as ArgumentException)?.ParamName);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateDeterministicGuid_PreservesUnknownVersionBehavior()
        {
            var version = (GuidManager.DeterministicGuidVersion)123;

            Guid expected = CreateReferenceDeterministicGuid(GuidManager.UrlNamespaceValue, "name", version);
            Guid actual = GuidManager.CreateDeterministicGuid(GuidManager.UrlNamespaceValue, "name", version);

            Assert.Equal(expected, actual);
            Assert.Equal(
                GuidManager.CreateDeterministicGuid(GuidManager.UrlNamespaceValue, "name", GuidManager.DeterministicGuidVersion.V3),
                actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateDeterministicGuid_MatchesReferenceImplementation_ForRandomInputs()
        {
            const string CharacterSet = "abcXYZ019-_/ \0é中\uD83D\uDE80";
            var random = new Random(3488);

            for (int iteration = 0; iteration < 2048; iteration++)
            {
                var namespaceBytes = new byte[16];
                random.NextBytes(namespaceBytes);
                Guid namespaceGuid = new Guid(namespaceBytes);
                string namespaceValue = FormatNamespace(namespaceGuid, iteration);
                var nameCharacters = new char[random.Next(1, 1025)];
                for (int characterIndex = 0; characterIndex < nameCharacters.Length; characterIndex++)
                {
                    nameCharacters[characterIndex] = CharacterSet[random.Next(CharacterSet.Length)];
                }

                string name = new string(nameCharacters);
                GuidManager.DeterministicGuidVersion version = iteration % 2 == 0
                    ? GuidManager.DeterministicGuidVersion.V3
                    : GuidManager.DeterministicGuidVersion.V5;

                Guid expected = CreateReferenceDeterministicGuid(namespaceValue, name, version);
                Guid actual = GuidManager.CreateDeterministicGuid(namespaceValue, name, version);

                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateDeterministicGuid_DoesNotAllocateForRepresentativeName()
        {
            const string Name = "instance_2026-07-27T20:30:10.3440000Z_42";
            const int Iterations = 1000;

            for (int iteration = 0; iteration < 10; iteration++)
            {
                GuidManager.CreateDeterministicGuid(GuidManager.UrlNamespaceValue, Name);
            }

            int checksum = 0;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                checksum += GuidManager.CreateDeterministicGuid(GuidManager.UrlNamespaceValue, Name).GetHashCode();
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            Assert.NotEqual(0, checksum);
            Assert.Equal(0, allocatedBytes);
        }

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

        private static Guid CreateReferenceDeterministicGuid(
            string namespaceValue,
            string name,
            GuidManager.DeterministicGuidVersion version)
        {
            if (string.IsNullOrEmpty(namespaceValue))
            {
                throw new ArgumentException("Please provide value for 'namespace'");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Please provide value for 'name'");
            }

            Guid namespaceValueGuid = Guid.Parse(namespaceValue);

            byte[] nameByteArray = Encoding.UTF8.GetBytes(name);
            byte[] namespaceValueByteArray = namespaceValueGuid.ToByteArray();
            SwapByteArrayValues(namespaceValueByteArray);

            byte[] hashByteArray;
            using (HashAlgorithm hashAlgorithm = version == GuidManager.DeterministicGuidVersion.V5
                ? (HashAlgorithm)SHA1.Create()
                : MD5.Create())
            {
                hashAlgorithm.TransformBlock(namespaceValueByteArray, 0, namespaceValueByteArray.Length, null, 0);
                hashAlgorithm.TransformFinalBlock(nameByteArray, 0, nameByteArray.Length);
                hashByteArray = hashAlgorithm.Hash;
            }

            var newGuidByteArray = new byte[16];
            Array.Copy(hashByteArray, 0, newGuidByteArray, 0, newGuidByteArray.Length);

            int versionValue = version == GuidManager.DeterministicGuidVersion.V5 ? 5 : 3;
            newGuidByteArray[6] = (byte)((newGuidByteArray[6] & 0x0F) | (versionValue << 4));
            newGuidByteArray[8] = (byte)((newGuidByteArray[8] & 0x3F) | 0x80);
            SwapByteArrayValues(newGuidByteArray);

            return new Guid(newGuidByteArray);
        }

        private static string FormatNamespace(Guid namespaceGuid, int iteration)
        {
            switch (iteration % 4)
            {
                case 0:
                    return namespaceGuid.ToString("D");
                case 1:
                    return namespaceGuid.ToString("B").ToUpperInvariant();
                case 2:
                    return namespaceGuid.ToString("N");
                default:
                    return namespaceGuid.ToString("P");
            }
        }

        private static void SwapByteArrayValues(byte[] byteArray)
        {
            SwapByteArrayElements(byteArray, 0, 3);
            SwapByteArrayElements(byteArray, 1, 2);
            SwapByteArrayElements(byteArray, 4, 5);
            SwapByteArrayElements(byteArray, 6, 7);
        }

        private static void SwapByteArrayElements(byte[] byteArray, int left, int right)
        {
            byte temp = byteArray[left];
            byteArray[left] = byteArray[right];
            byteArray[right] = temp;
        }
    }
}
