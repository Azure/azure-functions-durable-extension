// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;

namespace WebJobs.Extensions.DurableTask.CustomStorage
{
    /// <summary>
    /// Storage Options for the Custom Storage version of the extension.
    /// </summary>
    public class CustomStorageOptions : IStorageOptions
    {
        /// <inheritdoc/>

        public string ConnectionDetails => "Custom connection.";

        /// <inheritdoc/>

        public string StorageTypeName => "Custom";

        /// <inheritdoc/>
        public List<KeyValuePair<string, string>> GetValues()
        {
            return new List<KeyValuePair<string, string>>();
        }

        /// <inheritdoc/>

        public void Validate()
        {
            // NO OP
        }

        /// <inheritdoc/>

        public void ValidateHubName(string hubName)
        {
            // NO OP
        }
    }
}
