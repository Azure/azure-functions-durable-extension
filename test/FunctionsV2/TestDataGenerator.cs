// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TestDataGenerator
    {
        private static readonly object[] BoolOptions = new object[] { true, false };
        private static readonly object[] StorageProviderOptions = new object[] { TestHelpers.AzureStorageProviderType };

        public static IEnumerable<object[]> GetStorageProviderOptions()
        {
            foreach (object storageProviderOption in StorageProviderOptions)
            {
                yield return new object[] { storageProviderOption };
            }
        }

        public static IEnumerable<object[]> GetExtendedSessionAndStorageProviderOptions()
        {
            foreach (object boolOption in BoolOptions)
            {
                foreach (object storageProviderOption in StorageProviderOptions)
                {
                    yield return new object[] { boolOption, storageProviderOption };
                }
            }
        }
    }
}
