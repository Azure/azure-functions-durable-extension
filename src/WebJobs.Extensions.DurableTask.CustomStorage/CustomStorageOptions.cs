// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;

namespace WebJobs.Extensions.DurableTask.CustomStorage
{
    public class CustomStorageOptions : IStorageOptions
    {
        public string ConnectionDetails => "Custom connection.";

        public string StorageTypeName => "Custom";

        public List<KeyValuePair<string, string>> GetValues()
        {
            return new List<KeyValuePair<string, string>>();
        }

        public void Validate()
        {
            // NO OP
        }

        public void ValidateHubName(string hubName)
        {
            // NO OP
        }
    }
}
