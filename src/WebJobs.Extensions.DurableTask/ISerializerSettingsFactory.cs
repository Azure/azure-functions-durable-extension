// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to build instances of <see cref="JsonSerializerSettings"/>.
    /// </summary>
    public interface ISerializerSettingsFactory
    {
        /// <summary>
        /// Creates or retrieves JsonSerializerSettings to be used throughout the extension.
        /// </summary>
        /// <returns>JsonSerializerSettings to be used by the Durable Task Extension.</returns>
        JsonSerializerSettings CreateJsonSerializerSettings();
    }
}
