// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Class for creating deterministic <see cref="Guid"/>.
    /// </summary>
    internal static class GuidManager
    {
        private const int NamespaceByteCount = 16;
        private const int Sha1HashByteCount = 20;
        private const int StackBufferByteCount = 256;

        internal const string DnsNamespaceValue = "9e952958-5e33-4daf-827f-2fa12937b875";
        internal const string UrlNamespaceValue = "9e952959-5e33-4daf-827f-2fa12937b875";
        internal const string IsoOidNamespaceValue = "9e952960-5e33-4daf-827f-2fa12937b875";

        private static readonly Guid DnsNamespaceGuid = new Guid(DnsNamespaceValue);
        private static readonly Guid UrlNamespaceGuid = new Guid(UrlNamespaceValue);
        private static readonly Guid IsoOidNamespaceGuid = new Guid(IsoOidNamespaceValue);

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

            Guid namespaceValueGuid = ParseNamespace(namespaceValue);
            int nameByteCount = Encoding.UTF8.GetByteCount(name);
            int hashInputByteCount = NamespaceByteCount + nameByteCount;
            byte[] rentedBuffer = null;

            try
            {
                Span<byte> hashInput = hashInputByteCount <= StackBufferByteCount
                    ? stackalloc byte[hashInputByteCount]
                    : (rentedBuffer = ArrayPool<byte>.Shared.Rent(hashInputByteCount)).AsSpan(0, hashInputByteCount);
                Span<byte> namespaceBytes = hashInput.Slice(0, NamespaceByteCount);
                namespaceValueGuid.TryWriteBytes(namespaceBytes);
                SwapByteArrayValues(namespaceBytes);
                Encoding.UTF8.GetBytes(name.AsSpan(), hashInput.Slice(NamespaceByteCount));

                Span<byte> hashBytes = stackalloc byte[Sha1HashByteCount];
                if (version == DeterministicGuidVersion.V5)
                {
                    // CodeQL [SM02196] Suppressed: SHA1 is required for replay-compatible RFC 4122 V5 GUIDs.
                    SHA1.HashData(hashInput, hashBytes);
                }
                else
                {
                    // CodeQL [SM02196] Suppressed: MD5 is required for replay-compatible RFC 4122 V3 GUIDs.
                    MD5.HashData(hashInput, hashBytes);
                }

                int versionValue = version == DeterministicGuidVersion.V5 ? 5 : 3;
                hashBytes[6] = (byte)((hashBytes[6] & 0x0F) | (versionValue << 4));
                hashBytes[8] = (byte)((hashBytes[8] & 0x3F) | 0x80);
                Span<byte> guidBytes = hashBytes.Slice(0, NamespaceByteCount);
                SwapByteArrayValues(guidBytes);

                return new Guid(guidBytes);
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
        }

        private static Guid ParseNamespace(string namespaceValue)
        {
            if (namespaceValue == UrlNamespaceValue)
            {
                return UrlNamespaceGuid;
            }

            if (namespaceValue == DnsNamespaceValue)
            {
                return DnsNamespaceGuid;
            }

            if (namespaceValue == IsoOidNamespaceValue)
            {
                return IsoOidNamespaceGuid;
            }

            return Guid.Parse(namespaceValue);
        }

        private static void SwapByteArrayValues(Span<byte> byteArray)
        {
            SwapByteArrayElements(byteArray, 0, 3);
            SwapByteArrayElements(byteArray, 1, 2);
            SwapByteArrayElements(byteArray, 4, 5);
            SwapByteArrayElements(byteArray, 6, 7);
        }

        private static void SwapByteArrayElements(Span<byte> byteArray, int left, int right)
        {
            byte temp = byteArray[left];
            byteArray[left] = byteArray[right];
            byteArray[right] = temp;
        }
    }
}
