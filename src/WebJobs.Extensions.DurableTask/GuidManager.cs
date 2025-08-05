// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Class for creating deterministic <see cref="Guid"/>.
    /// </summary>
    internal static class GuidManager
    {
        internal const string DnsNamespaceValue = "9e952958-5e33-4daf-827f-2fa12937b875";
        internal const string UrlNamespaceValue = "9e952959-5e33-4daf-827f-2fa12937b875";
        internal const string IsoOidNamespaceValue = "9e952960-5e33-4daf-827f-2fa12937b875";

        internal enum DeterministicGuidVersion
        {
            V3,
            V5,
        }

        internal static Guid CreateDeterministicGuid(string namespaceValue, string name)
        {
            return CreateDeterministicGuid(namespaceValue, name, DeterministicGuidVersion.V5);
        }

        internal static Guid CreateDeterministicGuid(string namespaceValue, string name, DeterministicGuidVersion version)
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
            using (HashAlgorithm hashAlgorithm = version == DeterministicGuidVersion.V5
                ? (HashAlgorithm)SHA1.Create() /* CodeQL [SM02196] Suppressed: SHA1 is not used for cryptographic purposes here. The information being hashed is not sensitive,
                                                  and the goal is to generate a deterministic Guid. We cannot update to SHA2-based algorithms without breaking
                                                  customers' inflight orchestrations. */
                : MD5.Create()) /* CodeQL [SM02196] Suppressed: MD5 is not used for cryptographic purposes here. The information being hashed is not sensitive,
                                   and the goal is to generate a deterministic Guid. We cannot update to SHA2-based algorithms without breaking
                                   customers' inflight orchestrations. */
            {
                hashAlgorithm.TransformBlock(namespaceValueByteArray, 0, namespaceValueByteArray.Length, null, 0);
                hashAlgorithm.TransformFinalBlock(nameByteArray, 0, nameByteArray.Length);
                hashByteArray = hashAlgorithm.Hash;
            }

            byte[] newGuidByteArray = new byte[16];
            Array.Copy(hashByteArray, 0, newGuidByteArray, 0, 16);

            int versionValue = version == DeterministicGuidVersion.V5 ? 5 : 3;

            newGuidByteArray[6] = (byte)((newGuidByteArray[6] & 0x0F) | (versionValue << 4));

            newGuidByteArray[8] = (byte)((newGuidByteArray[8] & 0x3F) | 0x80);

            SwapByteArrayValues(newGuidByteArray);

            return new Guid(newGuidByteArray);
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
