// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Methods that all concrete storage options implementations must implement to work with
    /// core extension infrastructure.
    /// </summary>
    public interface IStorageOptions
    {
        /// <summary>
        /// Connections strings and other information used to connect to the backend storage provider.
        /// </summary>
        string ConnectionDetails { get; }

        /// <summary>
        /// Name of the storage provider to be used in debug strings.
        /// </summary>
        string StorageTypeName { get; }

        /// <summary>
        /// Throws an exception if the provided hub name violates any naming conventions for the storage provider.
        /// </summary>
        void ValidateHubName(string hubName);

        /// <summary>
        /// Throws an exception if any of the settings of the storage provider are invalid.
        /// </summary>
        void Validate();

        /// <summary>
        /// Retrieves a list of parameters and values to be used to generate debug strings.
        /// </summary>
        /// <returns>List of parameters and values.</returns>
        List<KeyValuePair<string, string>> GetValues();
    }
}
