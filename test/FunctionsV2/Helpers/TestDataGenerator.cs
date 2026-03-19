// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TestDataGenerator
    {
        private static readonly object[] BoolOptions = new object[] { true, false };
        private static readonly object[] FullFeaturedStorageProviders = new object[] { TestHelpers.AzureStorageProviderType };
        private static readonly object[] AllStorageProviders = new object[] { TestHelpers.AzureStorageProviderType, TestHelpers.RedisProviderType };

        public static IEnumerable<object[]> GetFullFeaturedStorageProviderOptions()
        {
            foreach (object storageProviderOption in FullFeaturedStorageProviders)
            {
                yield return new object[] { storageProviderOption };
            }
        }

        public static IEnumerable<object[]> GetBooleanAndFullFeaturedStorageProviderOptions()
        {
            foreach (object boolOption in BoolOptions)
            {
                foreach (object storageProviderOption in FullFeaturedStorageProviders)
                {
                    yield return new object[] { boolOption, storageProviderOption };
                }
            }
        }

        public static IEnumerable<object[]> GetAllStorageProviderOptions()
        {
            foreach (object storageProviderOption in AllStorageProviders)
            {
                yield return new object[] { storageProviderOption };
            }
        }

        public static IEnumerable<object[]> GetAllSupportedExtendedSessionWithStorageProviderOptions()
        {
            yield return new object[] { true, TestHelpers.AzureStorageProviderType };
            yield return new object[] { false, TestHelpers.AzureStorageProviderType };

            // Currently no support for extended sessions in Redis provider type.
            // yield return new object[] { true, TestHelpers.RedisProviderType };

            // Currently no support for Redis provider type in CI. Can uncomment out configuration below if have Redis setup locally.
            // yield return new object[] { false, TestHelpers.RedisProviderType };
        }
    }
}
