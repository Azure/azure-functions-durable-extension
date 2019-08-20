// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the Emulator storage provider.
    /// </summary>
    /// <remarks>
    /// At this time, there is no configuration available for the Emulator storage provider.
    /// </remarks>
    public class EmulatorStorageOptions : IStorageOptions
    {
        /// <inheritdoc />
        public string ConnectionDetails => string.Empty;

        /// <inheritdoc />
        public string StorageTypeName => "Emulator";


        void IStorageOptions.ValidateHubName(string hubName)
        {
            // NO-OP
        }

        void IStorageOptions.Validate()
        {
            // NO-OP
        }

        /// <inheritdoc />
        public List<KeyValuePair<string, string>> GetValues()
        {
            return new List<KeyValuePair<string, string>>();
        }
    }
}
